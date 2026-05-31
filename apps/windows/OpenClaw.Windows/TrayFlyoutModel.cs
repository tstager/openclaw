namespace OpenClaw.Windows;

/// <summary>
/// Action a tray flyout row invokes when activated.
/// </summary>
public enum TrayFlyoutAction
{
    OpenShell,
    OpenHome,
    OpenChat,
    OpenCanvas,
    OpenSessions,
    OpenApprovals,
    OpenPairing,
    OpenSettings,
    OpenLogs,
    Exit,
    ConnectRealtime,
    DisconnectRealtime,
    RunGatewayInstall,
    RunGatewayStart,
    RunGatewayStop,
    RunGatewayRestart,
    ToggleCanvasNode,
    ToggleVoiceControls,
    ToggleApprovalAlerts,
    TogglePairingAlerts,
    ToggleGatewayHealthAlerts,
    ToggleDevicePermissionAlerts,
}

/// <summary>
/// Semantic status-dot color so the flyout view picks the matching palette brush.
/// </summary>
public enum TrayStatusTone
{
    Neutral,
    Success,
    Caution,
    Critical,
    Accent,
}

/// <summary>
/// A status-dot row: a small colored dot, a label, and an optional right-aligned badge.
/// </summary>
public sealed record TrayStatusRow(string Label, string? Detail, TrayStatusTone Tone, string? Badge);

/// <summary>
/// An icon-plus-label action row, optionally carrying a right-aligned badge (e.g. a pending count).
/// </summary>
public sealed record TrayActionRow(string Label, string Glyph, TrayFlyoutAction Action, string? Badge = null);

/// <summary>
/// An icon-plus-label permission toggle row. Activating it flips a preference-backed capability without
/// dismissing the flyout, so it routes through the flyout's toggle channel rather than the action channel.
/// </summary>
public sealed record TrayToggleRow(string Label, string Glyph, bool IsOn, TrayFlyoutAction ToggleAction);

/// <summary>
/// A flyout section grouping status, action, and toggle rows under an optional heading. Sessions 3-6 add sections here.
/// </summary>
public sealed record TrayFlyoutSection(
    string? Heading,
    IReadOnlyList<TrayStatusRow> StatusRows,
    IReadOnlyList<TrayActionRow> ActionRows,
    IReadOnlyList<TrayToggleRow> ToggleRows)
{
    /// <summary>
    /// Creates a section with no toggle rows so existing status/action sections and their tests stay unchanged.
    /// </summary>
    public TrayFlyoutSection(
        string? Heading,
        IReadOnlyList<TrayStatusRow> StatusRows,
        IReadOnlyList<TrayActionRow> ActionRows)
        : this(Heading, StatusRows, ActionRows, [])
    {
    }
}

/// <summary>
/// The compact, ordered set of sections the tray flyout renders for a given snapshot.
/// </summary>
public sealed record TrayFlyoutModel(IReadOnlyList<TrayFlyoutSection> Sections);

/// <summary>
/// Maps a <see cref="WindowsTraySnapshot"/> to the flyout's status and action rows so the visual
/// composition can be unit tested without the WinUI runtime. Sessions 3-6 extend the section list here.
/// </summary>
public static class TrayFlyoutComposer
{
    // Segoe Fluent Icons glyphs, matching the shell navigation conventions.
    private const string OpenGlyph = "";
    private const string HomeGlyph = "";
    private const string ChatGlyph = "";
    private const string CanvasGlyph = "";
    private const string SessionsGlyph = "";
    private const string ApprovalsGlyph = "";
    private const string PairingGlyph = "";
    private const string SettingsGlyph = "";
    private const string LogsGlyph = "";
    private const string ExitGlyph = "";
    private const string ConnectGlyph = "";
    private const string DisconnectGlyph = "";
    private const string InstallGlyph = "";
    private const string StartGlyph = "";
    private const string RestartGlyph = "";
    private const string StopGlyph = "";
    private const string VoiceGlyph = "";
    private const string NotificationGlyph = "";

    /// <summary>
    /// The hard NotifyIcon tooltip limit; longer text is rejected by the shell, so the builder stays within it.
    /// </summary>
    public const int TooltipMaxLength = 63;

    /// <summary>
    /// Builds the concise (&lt;= 63 char) tray tooltip covering gateway state, node/canvas state, an optional
    /// warning count, and the latest activity. Activity is truncated first so the leading status survives the cap.
    /// </summary>
    /// <param name="snapshot">The current tray snapshot.</param>
    /// <param name="warningCount">Outstanding-work warnings (pending approvals + pairings + onboarding warnings).</param>
    public static string BuildTooltip(WindowsTraySnapshot snapshot, int warningCount)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var gateway = DescribeGateway(snapshot) + (snapshot.GatewayIsLocal ? " (Local)" : " (Remote)");
        var head = $"OpenClaw - {gateway} - Canvas {DescribeCanvas(snapshot.CanvasReadiness)}";
        if (warningCount > 0)
        {
            head += $" - {warningCount} warning{(warningCount == 1 ? string.Empty : "s")}";
        }

        if (head.Length >= TooltipMaxLength)
        {
            return Truncate(head, TooltipMaxLength);
        }

        if (string.IsNullOrWhiteSpace(snapshot.LatestActivity))
        {
            return head;
        }

        var withActivity = $"{head} - {snapshot.LatestActivity}";
        return Truncate(withActivity, TooltipMaxLength);
    }

    private static string Truncate(string value, int max)
    {
        return value.Length <= max ? value : value[..max];
    }

    /// <summary>
    /// Builds the ordered sections shown in the tray flyout from the current snapshot.
    /// </summary>
    public static TrayFlyoutModel Compose(WindowsTraySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var sections = new List<TrayFlyoutSection>
        {
            BuildStatusSection(snapshot),
            BuildQuickActionsSection(snapshot),
            BuildPermissionsSection(snapshot),
            BuildGatewaySection(snapshot),
        };

        return new TrayFlyoutModel(sections);
    }

    /// <summary>
    /// Live status-dot rows for the gateway, Canvas/A2UI node, sessions, and the latest activity.
    /// </summary>
    private static TrayFlyoutSection BuildStatusSection(WindowsTraySnapshot snapshot)
    {
        var statusRows = new List<TrayStatusRow>
        {
            new(
                Label: $"Gateway: {DescribeGateway(snapshot)}",
                Detail: snapshot.GatewayUrl,
                Tone: ResolveGatewayTone(snapshot),
                Badge: snapshot.GatewayIsLocal ? "Local" : "Remote"),
            new(
                Label: $"Canvas: {DescribeCanvas(snapshot.CanvasReadiness)}",
                Detail: null,
                Tone: ResolveCanvasTone(snapshot.CanvasReadiness),
                Badge: null),
        };

        if (snapshot.SessionCount > 0)
        {
            statusRows.Add(new TrayStatusRow(
                Label: $"Sessions: {snapshot.SessionCount}",
                Detail: null,
                Tone: TrayStatusTone.Accent,
                Badge: snapshot.SessionCount.ToString()));
        }

        if (!string.IsNullOrWhiteSpace(snapshot.LatestActivity))
        {
            statusRows.Add(new TrayStatusRow(
                Label: "Activity",
                Detail: snapshot.LatestActivity,
                Tone: TrayStatusTone.Neutral,
                Badge: null));
        }

        return new TrayFlyoutSection(Heading: null, StatusRows: statusRows, ActionRows: []);
    }

    /// <summary>
    /// Always-available navigation quick-actions. Home is the dashboard, so it is a single row.
    /// Approvals, Pairing, and Sessions carry their own right-aligned count badges (Sessions only when &gt; 0),
    /// so the shell entry no longer needs the combined pending badge. Sessions 5-6 grow this set.
    /// </summary>
    private static TrayFlyoutSection BuildQuickActionsSection(WindowsTraySnapshot snapshot)
    {
        var actionRows = new List<TrayActionRow>
        {
            new("Open OpenClaw", OpenGlyph, TrayFlyoutAction.OpenShell),
            new("Home", HomeGlyph, TrayFlyoutAction.OpenHome),
            new("Chat", ChatGlyph, TrayFlyoutAction.OpenChat),
            new("Canvas", CanvasGlyph, TrayFlyoutAction.OpenCanvas),
            new(
                "Sessions",
                SessionsGlyph,
                TrayFlyoutAction.OpenSessions,
                snapshot.SessionCount > 0 ? snapshot.SessionCount.ToString() : null),
            new(
                "Approvals",
                ApprovalsGlyph,
                TrayFlyoutAction.OpenApprovals,
                snapshot.PendingApprovalCount > 0 ? snapshot.PendingApprovalCount.ToString() : null),
            new(
                "Pairing",
                PairingGlyph,
                TrayFlyoutAction.OpenPairing,
                snapshot.PendingPairingCount > 0 ? snapshot.PendingPairingCount.ToString() : null),
            new("Settings", SettingsGlyph, TrayFlyoutAction.OpenSettings),
            new("Logs", LogsGlyph, TrayFlyoutAction.OpenLogs),
        };

        return new TrayFlyoutSection(Heading: null, StatusRows: [], ActionRows: actionRows);
    }

    /// <summary>
    /// Interactive toggles for the local, preference-backed capabilities: the Canvas/A2UI node, voice controls,
    /// and the four notification alert categories. Each row reflects the snapshot's current enablement and flips
    /// its preference through the flyout's toggle channel without dismissing the flyout. Device/screen/camera
    /// capabilities are intentionally absent because they have no preference backing to toggle.
    /// </summary>
    private static TrayFlyoutSection BuildPermissionsSection(WindowsTraySnapshot snapshot)
    {
        var toggleRows = new List<TrayToggleRow>
        {
            new("Canvas and A2UI node", CanvasGlyph, snapshot.CanvasNodeEnabled, TrayFlyoutAction.ToggleCanvasNode),
            new("Voice controls", VoiceGlyph, snapshot.VoiceControlsEnabled, TrayFlyoutAction.ToggleVoiceControls),
            new("Approval alerts", NotificationGlyph, snapshot.ApprovalAlertsEnabled, TrayFlyoutAction.ToggleApprovalAlerts),
            new("Pairing alerts", NotificationGlyph, snapshot.PairingAlertsEnabled, TrayFlyoutAction.TogglePairingAlerts),
            new("Gateway health alerts", NotificationGlyph, snapshot.GatewayHealthAlertsEnabled, TrayFlyoutAction.ToggleGatewayHealthAlerts),
            new("Device permission alerts", NotificationGlyph, snapshot.DevicePermissionAlertsEnabled, TrayFlyoutAction.ToggleDevicePermissionAlerts),
        };

        return new TrayFlyoutSection(Heading: "Permissions", StatusRows: [], ActionRows: [], ToggleRows: toggleRows);
    }

    /// <summary>
    /// Realtime connect/disconnect plus the snapshot's available gateway lifecycle actions, then Exit.
    /// </summary>
    private static TrayFlyoutSection BuildGatewaySection(WindowsTraySnapshot snapshot)
    {
        var actionRows = new List<TrayActionRow>();

        switch (snapshot.RealtimeAction)
        {
            case TrayRealtimeAction.Connect:
                actionRows.Add(new("Connect", ConnectGlyph, TrayFlyoutAction.ConnectRealtime));
                break;
            case TrayRealtimeAction.Disconnect:
                actionRows.Add(new("Disconnect", DisconnectGlyph, TrayFlyoutAction.DisconnectRealtime));
                break;
        }

        foreach (var gatewayAction in snapshot.AvailableGatewayActions)
        {
            actionRows.Add(MapGatewayAction(gatewayAction));
        }

        actionRows.Add(new("Exit", ExitGlyph, TrayFlyoutAction.Exit));

        return new TrayFlyoutSection(Heading: null, StatusRows: [], ActionRows: actionRows);
    }

    private static TrayActionRow MapGatewayAction(GatewayCliAction action)
    {
        return action switch
        {
            GatewayCliAction.Install => new("Install Gateway", InstallGlyph, TrayFlyoutAction.RunGatewayInstall),
            GatewayCliAction.Start => new("Start Gateway", StartGlyph, TrayFlyoutAction.RunGatewayStart),
            GatewayCliAction.Restart => new("Restart Gateway", RestartGlyph, TrayFlyoutAction.RunGatewayRestart),
            GatewayCliAction.Stop => new("Stop Gateway", StopGlyph, TrayFlyoutAction.RunGatewayStop),
            _ => new("Start Gateway", StartGlyph, TrayFlyoutAction.RunGatewayStart),
        };
    }

    private static string DescribeGateway(WindowsTraySnapshot snapshot)
    {
        return snapshot.GatewayRunning ? "Running" : snapshot.GatewayState;
    }

    private static TrayStatusTone ResolveGatewayTone(WindowsTraySnapshot snapshot)
    {
        if (snapshot.GatewayRunning && snapshot.RealtimeConnected)
        {
            return TrayStatusTone.Success;
        }

        if (snapshot.GatewayRunning)
        {
            return TrayStatusTone.Caution;
        }

        return TrayStatusTone.Critical;
    }

    private static string DescribeCanvas(TrayCanvasReadiness readiness)
    {
        return readiness switch
        {
            TrayCanvasReadiness.Ready => "Ready",
            TrayCanvasReadiness.Connecting => "Connecting",
            TrayCanvasReadiness.PairingRequired => "Pairing required",
            TrayCanvasReadiness.AuthFailed => "Auth failed",
            TrayCanvasReadiness.Disabled => "Disabled",
            _ => "Disconnected",
        };
    }

    private static TrayStatusTone ResolveCanvasTone(TrayCanvasReadiness readiness)
    {
        return readiness switch
        {
            TrayCanvasReadiness.Ready => TrayStatusTone.Success,
            TrayCanvasReadiness.Connecting => TrayStatusTone.Caution,
            TrayCanvasReadiness.PairingRequired => TrayStatusTone.Caution,
            TrayCanvasReadiness.AuthFailed => TrayStatusTone.Critical,
            _ => TrayStatusTone.Neutral,
        };
    }
}
