using System.Text.Json;

namespace OpenClaw.Windows;

/// <summary>
/// Stable command strings for the Windows node's secure system execution surface.
/// </summary>
public static class WindowsNodeSystemCommands
{
    public const string Which = "system.which";
    public const string RunPrepare = "system.run.prepare";
    public const string Run = "system.run";
}

/// <summary>
/// Parsed <c>system.*</c> invoke request.
/// </summary>
public sealed record WindowsSystemRunRequest(
    IReadOnlyList<string> Command,
    string? RawCommand,
    string? Cwd,
    IReadOnlyDictionary<string, string>? Env,
    int? TimeoutMs,
    string SessionKey,
    string RunId,
    string? AgentId,
    bool SuppressNotifyOnExit)
{
    public string Executable => this.Command.Count > 0 ? this.Command[0] : string.Empty;

    public string CommandText =>
        !string.IsNullOrWhiteSpace(this.RawCommand) ? this.RawCommand! : string.Join(' ', this.Command);
}

/// <summary>
/// Parses <c>system.*</c> invoke params into a <see cref="WindowsSystemRunRequest"/>.
/// </summary>
public static class WindowsSystemRunParser
{
    public static bool TryParse(
        string? paramsJson,
        string fallbackRunId,
        out WindowsSystemRunRequest request,
        out string? error)
    {
        request = new WindowsSystemRunRequest([], null, null, null, null, "node", fallbackRunId, null, false);
        error = null;
        if (string.IsNullOrWhiteSpace(paramsJson))
        {
            error = "system command params are required.";
            return false;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(paramsJson);
        }
        catch (JsonException)
        {
            error = "system command params must be valid JSON.";
            return false;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                error = "system command params must be a JSON object.";
                return false;
            }

            var command = ReadCommandArgv(root);
            if (command.Count == 0)
            {
                error = "command required";
                return false;
            }

            request = new WindowsSystemRunRequest(
                command,
                ReadString(root, "rawCommand"),
                ReadString(root, "cwd"),
                ReadStringMap(root, "env"),
                ReadInt(root, "timeoutMs"),
                ReadString(root, "sessionKey") is { Length: > 0 } sessionKey ? sessionKey : "node",
                ReadString(root, "runId") is { Length: > 0 } runId ? runId : fallbackRunId,
                ReadString(root, "agentId"),
                ReadBool(root, "suppressNotifyOnExit"));
            return true;
        }
    }

    private static IReadOnlyList<string> ReadCommandArgv(JsonElement root)
    {
        if (!root.TryGetProperty("command", out var command))
        {
            return [];
        }
        if (command.ValueKind == JsonValueKind.String)
        {
            var single = command.GetString();
            return string.IsNullOrWhiteSpace(single) ? [] : [single];
        }
        if (command.ValueKind != JsonValueKind.Array)
        {
            return [];
        }
        return command.EnumerateArray()
            .Where(static element => element.ValueKind == JsonValueKind.String)
            .Select(static element => element.GetString())
            .Where(static value => !string.IsNullOrEmpty(value))
            .Cast<string>()
            .ToArray();
    }

    private static IReadOnlyDictionary<string, string>? ReadStringMap(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.String)
            {
                map[property.Name] = property.Value.GetString() ?? string.Empty;
            }
        }
        return map.Count > 0 ? map : null;
    }

    private static string? ReadString(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static int? ReadInt(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var value) &&
               value.ValueKind == JsonValueKind.Number &&
               value.TryGetInt32(out var result)
            ? result
            : null;
    }

    private static bool ReadBool(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
    }
}

/// <summary>
/// Outcome of evaluating local Windows execution policy for a system command.
/// </summary>
public enum WindowsSystemRunDecisionKind
{
    Allowed,
    Denied,
    Invalid,
}

public sealed record WindowsSystemRunDecision(
    WindowsSystemRunDecisionKind Kind,
    string? Reason,
    string? Message)
{
    public static WindowsSystemRunDecision Allow() => new(WindowsSystemRunDecisionKind.Allowed, null, null);

    public static WindowsSystemRunDecision Deny(string reason, string message) =>
        new(WindowsSystemRunDecisionKind.Denied, reason, message);

    public static WindowsSystemRunDecision Invalid(string message) =>
        new(WindowsSystemRunDecisionKind.Invalid, "invalid", message);
}

/// <summary>
/// Evaluates the Windows node's local execution policy. Local enablement and the optional allowlist are defense in
/// depth on top of the gateway's admin-approved node pairing requirement.
/// </summary>
public static class WindowsSystemRunPolicy
{
    public static WindowsSystemRunDecision Evaluate(
        WindowsExecutionPolicyPreferences policy,
        WindowsSystemRunRequest request)
    {
        if (request.Command.Count == 0)
        {
            return WindowsSystemRunDecision.Invalid("command required");
        }
        if (!policy.AllowSystemExecution)
        {
            return WindowsSystemRunDecision.Deny(
                "security=deny",
                "SYSTEM_RUN_DENIED: system execution is disabled on this Windows node.");
        }
        if (policy.AllowedCommands.Count > 0)
        {
            var executable = NormalizeExecutableName(request.Executable);
            var allowed = policy.AllowedCommands.Any(candidate =>
                string.Equals(NormalizeExecutableName(candidate), executable, StringComparison.OrdinalIgnoreCase));
            if (!allowed)
            {
                return WindowsSystemRunDecision.Deny(
                    "allowlist-miss",
                    $"SYSTEM_RUN_DENIED: '{request.Executable}' is not in the Windows node command allowlist.");
            }
        }
        return WindowsSystemRunDecision.Allow();
    }

    public static string NormalizeExecutableName(string value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }
        var fileName = Path.GetFileName(trimmed);
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = trimmed;
        }
        var extension = Path.GetExtension(fileName);
        if (!string.IsNullOrEmpty(extension))
        {
            fileName = fileName[..^extension.Length];
        }
        return fileName.ToLowerInvariant();
    }
}

/// <summary>
/// Result of a Windows command execution.
/// </summary>
public sealed record WindowsCommandRunResult(
    int? ExitCode,
    bool TimedOut,
    bool Success,
    string Stdout,
    string Stderr,
    string? Error);

/// <summary>
/// Executes resolved Windows commands. Abstracted so the system command service is unit-testable.
/// </summary>
public interface IWindowsCommandExecutor
{
    Task<WindowsCommandRunResult> RunAsync(
        IReadOnlyList<string> command,
        string? cwd,
        IReadOnlyDictionary<string, string>? env,
        int? timeoutMs,
        CancellationToken cancellationToken);

    Task<string?> WhichAsync(string command, CancellationToken cancellationToken);
}

/// <summary>
/// Sends best-effort node events (exec.finished / exec.denied) to the gateway.
/// </summary>
public interface IWindowsNodeEventSink
{
    Task SendAsync(string @event, string payloadJson, CancellationToken cancellationToken);
}

/// <summary>
/// Handles the Windows node's <c>system.which</c>, <c>system.run.prepare</c>, and <c>system.run</c> commands behind
/// local execution policy, emitting <c>exec.finished</c>/<c>exec.denied</c> events and returning node.invoke results.
/// </summary>
public sealed class WindowsNodeSystemCommandService(
    WindowsExecutionPolicyPreferences policy,
    IWindowsCommandExecutor executor,
    IWindowsNodeEventSink events)
{
    private readonly WindowsExecutionPolicyPreferences policy = policy;
    private readonly IWindowsCommandExecutor executor = executor;
    private readonly IWindowsNodeEventSink events = events;

    public async Task<WindowsCanvasInvokeResponse> WhichAsync(
        WindowsCanvasInvokeRequest invoke,
        CancellationToken cancellationToken)
    {
        if (!TryParseRequest(invoke, out var request, out var error))
        {
            return WindowsCanvasInvokeResponse.Failure("INVALID_REQUEST", error!);
        }
        if (!this.policy.AllowSystemExecution)
        {
            return WindowsCanvasInvokeResponse.Failure(
                "UNAVAILABLE",
                "SYSTEM_RUN_DENIED: system execution is disabled on this Windows node.");
        }

        var path = await this.executor.WhichAsync(request.Executable, cancellationToken);
        return WindowsCanvasInvokeResponse.Success(JsonSerializer.Serialize(new
        {
            command = request.Executable,
            found = path is not null,
            path,
        }));
    }

    public Task<WindowsCanvasInvokeResponse> PrepareAsync(
        WindowsCanvasInvokeRequest invoke,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        if (!TryParseRequest(invoke, out var request, out var error))
        {
            return Task.FromResult(WindowsCanvasInvokeResponse.Failure("INVALID_REQUEST", error!));
        }

        var decision = WindowsSystemRunPolicy.Evaluate(this.policy, request);
        if (decision.Kind == WindowsSystemRunDecisionKind.Invalid)
        {
            return Task.FromResult(WindowsCanvasInvokeResponse.Failure("INVALID_REQUEST", decision.Message!));
        }

        return Task.FromResult(WindowsCanvasInvokeResponse.Success(JsonSerializer.Serialize(new
        {
            command = request.Command,
            commandText = request.CommandText,
            cwd = request.Cwd,
            allowed = decision.Kind == WindowsSystemRunDecisionKind.Allowed,
            reason = decision.Reason,
        })));
    }

    public async Task<WindowsCanvasInvokeResponse> RunAsync(
        WindowsCanvasInvokeRequest invoke,
        CancellationToken cancellationToken)
    {
        if (!TryParseRequest(invoke, out var request, out var error))
        {
            return WindowsCanvasInvokeResponse.Failure("INVALID_REQUEST", error!);
        }

        var decision = WindowsSystemRunPolicy.Evaluate(this.policy, request);
        if (decision.Kind == WindowsSystemRunDecisionKind.Invalid)
        {
            return WindowsCanvasInvokeResponse.Failure("INVALID_REQUEST", decision.Message!);
        }
        if (decision.Kind == WindowsSystemRunDecisionKind.Denied)
        {
            await this.EmitDeniedAsync(request, decision.Reason!, cancellationToken);
            return WindowsCanvasInvokeResponse.Failure("UNAVAILABLE", decision.Message!);
        }

        var timeoutMs = request.TimeoutMs is > 0 ? request.TimeoutMs : this.policy.DefaultTimeoutMs;
        var result = await this.executor.RunAsync(
            request.Command,
            request.Cwd,
            request.Env,
            timeoutMs,
            cancellationToken);
        await this.EmitFinishedAsync(request, result, cancellationToken);
        return WindowsCanvasInvokeResponse.Success(JsonSerializer.Serialize(new
        {
            exitCode = result.ExitCode,
            timedOut = result.TimedOut,
            success = result.Success,
            stdout = result.Stdout,
            stderr = result.Stderr,
            error = result.Error,
        }));
    }

    private static bool TryParseRequest(
        WindowsCanvasInvokeRequest invoke,
        out WindowsSystemRunRequest request,
        out string? error)
    {
        var fallbackRunId = string.IsNullOrWhiteSpace(invoke.Id) ? Guid.NewGuid().ToString("N") : invoke.Id;
        return WindowsSystemRunParser.TryParse(invoke.ParamsJson, fallbackRunId, out request, out error);
    }

    private Task EmitDeniedAsync(WindowsSystemRunRequest request, string reason, CancellationToken cancellationToken)
    {
        return this.events.SendAsync(
            "exec.denied",
            JsonSerializer.Serialize(new
            {
                sessionKey = request.SessionKey,
                runId = request.RunId,
                host = "node",
                command = request.CommandText,
                reason,
                suppressNotifyOnExit = request.SuppressNotifyOnExit,
            }),
            cancellationToken);
    }

    private Task EmitFinishedAsync(
        WindowsSystemRunRequest request,
        WindowsCommandRunResult result,
        CancellationToken cancellationToken)
    {
        var output = string.Join(
            '\n',
            new[] { result.Stdout, result.Stderr, result.Error }
                .Where(value => !string.IsNullOrEmpty(value)));
        return this.events.SendAsync(
            "exec.finished",
            JsonSerializer.Serialize(new
            {
                sessionKey = request.SessionKey,
                runId = request.RunId,
                host = "node",
                command = request.CommandText,
                exitCode = result.ExitCode,
                timedOut = result.TimedOut,
                success = result.Success,
                output,
                suppressNotifyOnExit = request.SuppressNotifyOnExit,
            }),
            cancellationToken);
    }
}

/// <summary>
/// Forwards node events to the Windows node transport, swallowing failures so best-effort events never break a run.
/// </summary>
public sealed class CanvasNodeEventSink(WindowsCanvasNodeClient node) : IWindowsNodeEventSink
{
    private readonly WindowsCanvasNodeClient node = node;

    public async Task SendAsync(string @event, string payloadJson, CancellationToken cancellationToken)
    {
        try
        {
            await this.node.SendEventAsync(@event, payloadJson, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            CrashLog.Write(ex);
        }
    }
}
