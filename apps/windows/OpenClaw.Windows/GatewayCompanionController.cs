using System.Text.Json;

namespace OpenClaw.Windows;

/// <summary>
/// Gateway lifecycle operations exposed by the Windows app.
/// </summary>
public enum GatewayCliAction
{
    Install,
    Start,
    Stop,
    Restart,
}

/// <summary>
/// Translates app actions and preferences into OpenClaw CLI commands and parses their JSON results.
/// </summary>
public sealed class GatewayCompanionController(
    IGatewayCliCommandRunner commandRunner,
    AppPreferencesStore preferences)
{
    private readonly IGatewayCliCommandRunner commandRunner = commandRunner;
    private readonly AppPreferencesStore preferences = preferences;

    /// <summary>
    /// Probes gateway status, applies compatibility fallbacks, and records the last check time.
    /// </summary>
    public async Task<GatewayStatusSnapshot> RefreshStatusAsync(CancellationToken cancellationToken = default)
    {
        var currentPreferences = await this.preferences.LoadAsync(cancellationToken);
        var result = await this.commandRunner.RunAsync(
            BuildGatewayStatusArgs(currentPreferences),
            cancellationToken);
        var snapshot = GatewayStatusSnapshot.FromCliResult(result);
        snapshot = ApplyDashboardUrlFallback(snapshot, currentPreferences);
        await this.preferences.UpdateAsync(current => current with
        {
            LastStatus = snapshot.State,
            LastStatusCheckedAt = DateTimeOffset.UtcNow,
        }, cancellationToken);
        return snapshot;
    }

    /// <summary>
    /// Runs an install/start/stop/restart command and refreshes status after successful actions.
    /// </summary>
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

    /// <summary>
    /// Derives a dashboard URL from saved settings for older global CLIs that do not emit one.
    /// </summary>
    public static GatewayStatusSnapshot ApplyDashboardUrlFallback(
        GatewayStatusSnapshot snapshot,
        AppPreferences preferences)
    {
        if (!string.IsNullOrWhiteSpace(snapshot.DashboardUrl))
        {
            return snapshot;
        }

        var dashboardUrl = GatewayStatusSnapshot.DeriveDashboardUrl(preferences.GatewayUrl);
        return string.IsNullOrWhiteSpace(dashboardUrl) ? snapshot : snapshot with { DashboardUrl = dashboardUrl };
    }

    /// <summary>
    /// Builds the status command with optional URL/token probe auth from preferences.
    /// </summary>
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

/// <summary>
/// Result returned to the UI after a gateway lifecycle command.
/// </summary>
public sealed record GatewayActionResult(
    GatewayCliAction Action,
    bool Succeeded,
    string Output,
    GatewayStatusSnapshot Status);

/// <summary>
/// Normalized gateway status fields parsed from multiple CLI JSON versions.
/// </summary>
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
    /// <summary>
    /// Converts process output into a status snapshot, preserving failed output as the error detail.
    /// </summary>
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

    /// <summary>
    /// Parses current and older gateway status JSON shapes into the stable Windows UI model.
    /// </summary>
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
            ReadString(root, "service", "runtime", "status") ??
            ReadString(root, "status") ??
            (reachable ? "running" : "unknown");

        return new GatewayStatusSnapshot(
            State: state,
            ServiceInstalled: ResolveServiceInstalled(root),
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
                ReadString(root, "url") ??
                DeriveDashboardUrl(root, primaryTarget),
            LogPath:
                ReadString(root, "logFile") ??
                ReadString(root, "logs", "file") ??
                ReadString(root, "log", "path") ??
                ReadString(root, "logPath"),
            AuthWarning: ReadString(root, "rpc", "authWarning"),
            Error: ReadString(root, "error") ?? ReadString(root, "message"),
            RawJson: json);
    }

    private static bool ResolveServiceInstalled(JsonElement root)
    {
        if (ReadBool(root, "service", "installed") is { } installed)
        {
            return installed;
        }

        if (ReadBool(root, "service", "loaded") is { } loaded)
        {
            return loaded;
        }

        return !string.Equals(
            ReadString(root, "service", "state"),
            "not_installed",
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Selects the CLI's primary target, or the first target when older JSON lacks primaryTargetId.
    /// </summary>
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

    private static string? DeriveDashboardUrl(JsonElement root, JsonElement? primaryTarget)
    {
        var gatewayUrl =
            ReadString(root, "network", "localLoopbackUrl") ??
            ReadString(primaryTarget, "url");
        var basePath = ReadString(primaryTarget, "config", "gateway", "controlUiBasePath");
        return DeriveDashboardUrl(gatewayUrl, basePath);
    }

    /// <summary>
    /// Converts a gateway WebSocket URL into the matching Control UI HTTP URL.
    /// </summary>
    internal static string? DeriveDashboardUrl(string? gatewayUrl, string? basePath = null)
    {
        if (string.IsNullOrWhiteSpace(gatewayUrl) ||
            !Uri.TryCreate(gatewayUrl, UriKind.Absolute, out var parsed) ||
            !IsWebSocketScheme(parsed.Scheme))
        {
            return null;
        }

        var builder = new UriBuilder(parsed)
        {
            Scheme = string.Equals(parsed.Scheme, "wss", StringComparison.OrdinalIgnoreCase) ? Uri.UriSchemeHttps : Uri.UriSchemeHttp,
            Path = FormatControlUiPath(basePath),
            Query = string.Empty,
            Fragment = string.Empty,
        };
        return builder.Uri.ToString();
    }

    private static bool IsWebSocketScheme(string scheme)
    {
        return string.Equals(scheme, "ws", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(scheme, "wss", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatControlUiPath(string? basePath)
    {
        if (string.IsNullOrWhiteSpace(basePath) || string.Equals(basePath.Trim(), "/", StringComparison.Ordinal))
        {
            return "/";
        }

        var trimmed = basePath.Trim().Trim('/');
        return $"/{trimmed}/";
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
