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
        var result = await this.commandRunner.RunAsync(["gateway", "status", "--json"], cancellationToken);
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
        var args = action switch
        {
            GatewayCliAction.Install => new[] { "gateway", "install", "--json" },
            GatewayCliAction.Start => new[] { "gateway", "start", "--json" },
            GatewayCliAction.Stop => new[] { "gateway", "stop", "--json" },
            GatewayCliAction.Restart => new[] { "gateway", "restart", "--json" },
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, null),
        };
        var result = await this.commandRunner.RunAsync(args, cancellationToken);
        var status = await this.RefreshStatusAsync(cancellationToken);
        return new GatewayActionResult(action, result.Succeeded, result.CombinedOutput, status);
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
        var state =
            ReadString(root, "runtime", "state") ??
            ReadString(root, "service", "state") ??
            ReadString(root, "status") ??
            (ReadBool(root, "ok") == true ? "running" : "unknown");

        var reachable =
            ReadBool(root, "rpc", "ok") ??
            ReadBool(root, "probe", "ok") ??
            ReadBool(root, "ok") ??
            false;

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
