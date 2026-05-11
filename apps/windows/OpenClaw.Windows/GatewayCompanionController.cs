using System.Text.Json;

namespace OpenClaw.Windows;

public enum GatewayCliAction
{
    Install,
    Start,
    Stop,
    Restart,
}

public sealed class GatewayCompanionController(
    IGatewayCliCommandRunner commandRunner,
    AppPreferencesStore preferences)
{
    private readonly IGatewayCliCommandRunner commandRunner = commandRunner;
    private readonly AppPreferencesStore preferences = preferences;

    public async Task<GatewayStatusSnapshot> RefreshStatusAsync(CancellationToken cancellationToken = default)
    {
        var currentPreferences = await this.preferences.LoadAsync(cancellationToken);
        var result = await this.commandRunner.RunAsync(
            BuildGatewayStatusArgs(currentPreferences),
            cancellationToken);
        var snapshot = GatewayStatusSnapshot.FromCliResult(result);
        await this.preferences.UpdateAsync(current => current with
        {
            LastStatus = snapshot.State,
            LastStatusCheckedAt = DateTimeOffset.UtcNow,
        }, cancellationToken);
        return snapshot;
    }

    public async Task<GatewayActionResult> RunActionAsync(
        GatewayCliAction action,
        CancellationToken cancellationToken = default)
    {
        var currentPreferences = await this.preferences.LoadAsync(cancellationToken);
        var args = action switch
        {
            GatewayCliAction.Install => BuildGatewayInstallArgs(currentPreferences),
            GatewayCliAction.Start => new[] { "gateway", "start", "--json" },
            GatewayCliAction.Stop => new[] { "gateway", "stop", "--json" },
            GatewayCliAction.Restart => new[] { "gateway", "restart", "--json" },
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, null),
        };
        var result = await this.commandRunner.RunAsync(args, cancellationToken);
        if (!result.Succeeded)
        {
            var failedStatus = GatewayStatusSnapshot.FromCliResult(result);
            await this.preferences.UpdateAsync(current => current with
            {
                LastStatus = failedStatus.State,
                LastStatusCheckedAt = DateTimeOffset.UtcNow,
            }, cancellationToken);
            return new GatewayActionResult(action, false, result.CombinedOutput, failedStatus);
        }

        var status = await this.RefreshStatusAsync(cancellationToken);
        return new GatewayActionResult(action, result.Succeeded, result.CombinedOutput, status);
    }

    public static IReadOnlyList<string> BuildGatewayStatusArgs(AppPreferences preferences)
    {
        var args = new List<string> { "gateway", "status", "--json" };
        AppendGatewayProbeAuthArgs(args, preferences);
        return args;
    }

    private static IReadOnlyList<string> BuildGatewayInstallArgs(AppPreferences preferences)
    {
        var args = new List<string> { "gateway", "install", "--json" };
        AppendGatewayTokenArg(args, preferences);
        return args;
    }

    private static void AppendGatewayProbeAuthArgs(List<string> args, AppPreferences preferences)
    {
        if (string.IsNullOrWhiteSpace(preferences.GatewayToken))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(preferences.GatewayUrl))
        {
            args.Add("--url");
            args.Add(preferences.GatewayUrl.Trim());
        }

        AppendGatewayTokenArg(args, preferences);
    }

    private static void AppendGatewayTokenArg(List<string> args, AppPreferences preferences)
    {
        if (!string.IsNullOrWhiteSpace(preferences.GatewayToken))
        {
            args.Add("--token");
            args.Add(preferences.GatewayToken.Trim());
        }
    }
}

public sealed record GatewayActionResult(
    GatewayCliAction Action,
    bool Succeeded,
    string Output,
    GatewayStatusSnapshot Status);

public sealed record GatewayStatusSnapshot(
    string State,
    bool ServiceInstalled,
    bool Reachable,
    string Capability,
    string? DashboardUrl,
    string? LogPath,
    string? AuthWarning,
    string? Error,
    string RawJson)
{
    public static GatewayStatusSnapshot FromCliResult(GatewayCliResult result)
    {
        if (!result.Succeeded)
        {
            return new GatewayStatusSnapshot(
                State: "unavailable",
                ServiceInstalled: false,
                Reachable: false,
                Capability: "unknown",
                DashboardUrl: null,
                LogPath: null,
                AuthWarning: null,
                Error: result.CombinedOutput,
                RawJson: result.StandardOutput);
        }

        return FromJson(result.StandardOutput);
    }

    public static GatewayStatusSnapshot FromJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var primaryTarget = ReadPrimaryTarget(root);

        var reachable =
            ReadBool(root, "rpc", "ok") ??
            ReadBool(root, "probe", "ok") ??
            ReadBool(primaryTarget, "connect", "ok") ??
            ReadBool(primaryTarget, "connect", "rpcOk") ??
            ReadBool(root, "ok") ??
            false;

        var state =
            ReadString(root, "runtime", "state") ??
            ReadString(root, "service", "state") ??
            ReadString(root, "status") ??
            (reachable ? "running" : "unknown");

        return new GatewayStatusSnapshot(
            State: state,
            ServiceInstalled:
                ReadBool(root, "service", "installed") ??
                !string.Equals(ReadString(root, "service", "state"), "not_installed", StringComparison.OrdinalIgnoreCase),
            Reachable: reachable,
            Capability:
                ReadString(root, "rpc", "capability") ??
                ReadString(root, "probe", "capability") ??
                ReadString(root, "capability") ??
                ReadString(primaryTarget, "auth", "capability") ??
                "unknown",
            DashboardUrl:
                ReadString(root, "dashboard", "url") ??
                ReadString(root, "controlUi", "url") ??
                ReadString(root, "url"),
            LogPath:
                ReadString(root, "logs", "file") ??
                ReadString(root, "log", "path") ??
                ReadString(root, "logPath"),
            AuthWarning: ReadString(root, "rpc", "authWarning"),
            Error: ReadString(root, "error") ?? ReadString(root, "message"),
            RawJson: json);
    }

    private static JsonElement? ReadPrimaryTarget(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("targets", out var targets) ||
            targets.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var primaryTargetId = ReadString(root, "primaryTargetId");
        JsonElement? firstTarget = null;
        foreach (var target in targets.EnumerateArray())
        {
            firstTarget ??= target;
            if (!string.IsNullOrWhiteSpace(primaryTargetId) &&
                string.Equals(ReadString(target, "id"), primaryTargetId, StringComparison.OrdinalIgnoreCase))
            {
                return target;
            }
        }

        return firstTarget;
    }

    private static bool? ReadBool(JsonElement root, params string[] path)
    {
        var value = Read(root, path);
        return value?.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }

    private static string? ReadString(JsonElement root, params string[] path)
    {
        var value = Read(root, path);
        return value?.ValueKind == JsonValueKind.String ? value.Value.GetString() : null;
    }

    private static bool? ReadBool(JsonElement? root, params string[] path)
    {
        return root is null ? null : ReadBool(root.Value, path);
    }

    private static string? ReadString(JsonElement? root, params string[] path)
    {
        return root is null ? null : ReadString(root.Value, path);
    }

    private static JsonElement? Read(JsonElement root, IReadOnlyList<string> path)
    {
        var current = root;
        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
            {
                return null;
            }
        }
        return current;
    }
}
