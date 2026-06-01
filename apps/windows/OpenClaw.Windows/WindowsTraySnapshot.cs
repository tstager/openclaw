namespace OpenClaw.Windows;

/// <summary>
/// Realtime connection action the tray can offer for the operator channel.
/// </summary>
public enum TrayRealtimeAction
{
    None,
    Connect,
    Disconnect,
}

/// <summary>
/// Readiness of the Windows Canvas/A2UI node as shown in the tray.
/// </summary>
public enum TrayCanvasReadiness
{
    Disabled,
    Disconnected,
    Connecting,
    PairingRequired,
    AuthFailed,
    Ready,
}

/// <summary>
/// Display-ready value model the tray flyout renders without reading from <c>MainWindow</c>.
/// </summary>
public sealed record WindowsTraySnapshot(
    string GatewayState,
    string GatewayUrl,
    bool GatewayRunning,
    bool GatewayIsLocal,
    string ConnectionState,
    bool RealtimeConnected,
    TrayRealtimeAction RealtimeAction,
    TrayCanvasReadiness CanvasReadiness,
    int SessionCount,
    int PendingApprovalCount,
    int PendingPairingCount,
    bool NotificationsEnabled,
    bool CanvasNodeEnabled,
    bool VoiceControlsEnabled,
    bool ApprovalAlertsEnabled,
    bool PairingAlertsEnabled,
    bool GatewayHealthAlertsEnabled,
    bool DevicePermissionAlertsEnabled,
    string? LatestActivity,
    WindowsThemePreference Theme,
    WindowsAccentColorPreference Accent,
    WindowsColorThemePreference ColorTheme,
    IReadOnlyList<GatewayCliAction> AvailableGatewayActions,
    IReadOnlyList<TrayNodeRow> Nodes,
    int ConnectedNodeCount,
    int PairedNodeCount)
{
    /// <summary>
    /// Builds the tray snapshot from live gateway, realtime, node, and preference state.
    /// </summary>
    public static WindowsTraySnapshot Create(
        GatewayStatusSnapshot? status,
        GatewayRealtimeState realtimeState,
        WindowsCanvasNodeState canvasNodeState,
        bool canvasNodeEnabled,
        AppPreferences preferences,
        int sessionCount,
        int pendingApprovalCount,
        int pendingPairingCount,
        string? lastActivity,
        WindowsNotificationActivity? latestNotification,
        IReadOnlyList<GatewayNodeSummary>? nodes = null,
        string? localNodeId = null)
    {
        var gatewayRunning = ResolveGatewayRunning(status);
        var realtimeConnected = realtimeState == GatewayRealtimeState.Connected;
        var gatewayUrl = status?.DashboardUrl ?? preferences.GatewayUrl;
        var nodeRows = ResolveNodeRows(nodes, localNodeId, canvasNodeState, canvasNodeEnabled);

        return new WindowsTraySnapshot(
            GatewayState: status?.State ?? "unknown",
            GatewayUrl: gatewayUrl,
            GatewayRunning: gatewayRunning,
            GatewayIsLocal: ResolveGatewayIsLocal(gatewayUrl),
            ConnectionState: realtimeState.ToString(),
            RealtimeConnected: realtimeConnected,
            RealtimeAction: ResolveRealtimeAction(realtimeState),
            CanvasReadiness: ResolveCanvasReadiness(canvasNodeState, canvasNodeEnabled),
            SessionCount: Math.Max(0, sessionCount),
            PendingApprovalCount: Math.Max(0, pendingApprovalCount),
            PendingPairingCount: Math.Max(0, pendingPairingCount),
            NotificationsEnabled: AnyNotificationsEnabled(preferences.NotificationPreferences),
            CanvasNodeEnabled: canvasNodeEnabled,
            VoiceControlsEnabled: preferences.VoiceControlsEnabled,
            ApprovalAlertsEnabled: preferences.NotificationPreferences.ApprovalAlerts,
            PairingAlertsEnabled: preferences.NotificationPreferences.PairingAlerts,
            GatewayHealthAlertsEnabled: preferences.NotificationPreferences.GatewayHealthAlerts,
            DevicePermissionAlertsEnabled: preferences.NotificationPreferences.DevicePermissionAlerts,
            LatestActivity: ResolveLatestActivity(lastActivity, latestNotification),
            Theme: preferences.ThemePreference,
            Accent: preferences.AccentColorPreference,
            ColorTheme: preferences.ColorThemePreference,
            AvailableGatewayActions: ResolveGatewayActions(status, gatewayRunning),
            Nodes: nodeRows,
            ConnectedNodeCount: nodeRows.Count(node => node.Online),
            PairedNodeCount: nodeRows.Count(node => node.Paired));
    }

    /// <summary>
    /// Projects gateway-reported nodes into tray rows, marking this Windows node local and synthesizing a local row
    /// from canvas state when the gateway has not yet reported it.
    /// </summary>
    private static IReadOnlyList<TrayNodeRow> ResolveNodeRows(
        IReadOnlyList<GatewayNodeSummary>? nodes,
        string? localNodeId,
        WindowsCanvasNodeState canvasNodeState,
        bool canvasNodeEnabled)
    {
        var rows = new List<TrayNodeRow>();
        var sawLocal = false;
        foreach (var node in nodes ?? [])
        {
            var isLocal = localNodeId is not null &&
                string.Equals(node.NodeId, localNodeId, StringComparison.Ordinal);
            sawLocal |= isLocal;
            rows.Add(new TrayNodeRow(
                Name: isLocal ? "This Windows node" : node.DisplayName,
                Platform: isLocal ? "windows" : node.Platform,
                Online: node.Connected,
                Paired: node.Paired,
                IsLocal: isLocal));
        }

        if (canvasNodeEnabled && !sawLocal)
        {
            var online = canvasNodeState == WindowsCanvasNodeState.Connected;
            rows.Insert(0, new TrayNodeRow(
                Name: "This Windows node",
                Platform: "windows",
                Online: online,
                Paired: online,
                IsLocal: true));
        }

        return rows;
    }

    /// <summary>
    /// Treats the gateway as running when the CLI reports it reachable or in a running runtime state.
    /// </summary>
    private static bool ResolveGatewayRunning(GatewayStatusSnapshot? status)
    {
        if (status is null)
        {
            return false;
        }

        if (status.Reachable)
        {
            return true;
        }

        return status.State is "running" or "started" or "active" or "online";
    }

    /// <summary>
    /// Treats a gateway whose host is a loopback address (127.0.0.1, localhost, ::1) as local, otherwise remote.
    /// </summary>
    private static bool ResolveGatewayIsLocal(string gatewayUrl)
    {
        if (string.IsNullOrWhiteSpace(gatewayUrl))
        {
            return false;
        }

        var host = Uri.TryCreate(gatewayUrl, UriKind.Absolute, out var uri)
            ? uri.Host
            : gatewayUrl;

        return host is "127.0.0.1" or "localhost" or "::1" or "[::1]";
    }

    /// <summary>
    /// Offers Disconnect on a live socket and Connect whenever the operator channel is not connected.
    /// </summary>
    private static TrayRealtimeAction ResolveRealtimeAction(GatewayRealtimeState realtimeState)
    {
        return realtimeState switch
        {
            GatewayRealtimeState.Connected => TrayRealtimeAction.Disconnect,
            GatewayRealtimeState.Connecting => TrayRealtimeAction.None,
            _ => TrayRealtimeAction.Connect,
        };
    }

    private static TrayCanvasReadiness ResolveCanvasReadiness(WindowsCanvasNodeState state, bool enabled)
    {
        if (!enabled)
        {
            return TrayCanvasReadiness.Disabled;
        }

        return state switch
        {
            WindowsCanvasNodeState.Connected => TrayCanvasReadiness.Ready,
            WindowsCanvasNodeState.Connecting => TrayCanvasReadiness.Connecting,
            WindowsCanvasNodeState.PairingRequired => TrayCanvasReadiness.PairingRequired,
            WindowsCanvasNodeState.AuthFailed => TrayCanvasReadiness.AuthFailed,
            _ => TrayCanvasReadiness.Disconnected,
        };
    }

    /// <summary>
    /// Derives the gateway lifecycle actions that make sense for the current state so the flyout only renders them.
    /// </summary>
    private static IReadOnlyList<GatewayCliAction> ResolveGatewayActions(
        GatewayStatusSnapshot? status,
        bool gatewayRunning)
    {
        if (status is null)
        {
            return [];
        }

        var actions = new List<GatewayCliAction>();
        if (!status.ServiceInstalled)
        {
            actions.Add(GatewayCliAction.Install);
        }

        if (gatewayRunning)
        {
            actions.Add(GatewayCliAction.Restart);
            actions.Add(GatewayCliAction.Stop);
        }
        else
        {
            actions.Add(GatewayCliAction.Start);
        }

        return actions;
    }

    private static bool AnyNotificationsEnabled(WindowsNotificationPreferences preferences)
    {
        return preferences.ApprovalAlerts ||
            preferences.PairingAlerts ||
            preferences.GatewayHealthAlerts ||
            preferences.DevicePermissionAlerts;
    }

    /// <summary>
    /// Prefers the coordinator's last activity text and falls back to the newest tray notification.
    /// </summary>
    private static string? ResolveLatestActivity(
        string? lastActivity,
        WindowsNotificationActivity? latestNotification)
    {
        if (!string.IsNullOrWhiteSpace(lastActivity))
        {
            return lastActivity;
        }

        return latestNotification is null
            ? null
            : $"{latestNotification.Title}: {latestNotification.Message}";
    }
}
