namespace OpenClaw.Windows;

/// <summary>
/// Action a tray flyout row invokes when activated.
/// </summary>
public enum TrayFlyoutAction
{
    OpenShell,
    OpenSettings,
    OpenLogs,
    Exit,
    ConnectRealtime,
    DisconnectRealtime,
    RunGatewayInstall,
    RunGatewayStart,
    RunGatewayStop,
    RunGatewayRestart,
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
/// A flyout section grouping status and action rows under an optional heading. Sessions 3-6 add sections here.
/// </summary>
public sealed record TrayFlyoutSection(
    string? Heading,
    IReadOnlyList<TrayStatusRow> StatusRows,
    IReadOnlyList<TrayActionRow> ActionRows);

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
    private const string SettingsGlyph = "";
    private const string LogsGlyph = "";
    private const string ExitGlyph = "";
    private const string ConnectGlyph = "";
    private const string DisconnectGlyph = "";
    private const string InstallGlyph = "";
    private const string StartGlyph = "";
    private const string RestartGlyph = "";
    private const string StopGlyph = "";

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
            BuildGatewaySection(snapshot),
        };

        return new TrayFlyoutModel(sections);
    }

    /// <summary>
    /// Status-dot rows for the gateway and Canvas/A2UI node readiness.
    /// </summary>
    private static TrayFlyoutSection BuildStatusSection(WindowsTraySnapshot snapshot)
    {
        var statusRows = new List<TrayStatusRow>
        {
            new(
                Label: $"Gateway: {DescribeGateway(snapshot)}",
                Detail: snapshot.GatewayUrl,
                Tone: ResolveGatewayTone(snapshot),
                Badge: null),
            new(
                Label: $"Canvas: {DescribeCanvas(snapshot.CanvasReadiness)}",
                Detail: null,
                Tone: ResolveCanvasTone(snapshot.CanvasReadiness),
                Badge: null),
        };

        return new TrayFlyoutSection(Heading: null, StatusRows: statusRows, ActionRows: []);
    }

    /// <summary>
    /// Always-available navigation actions. Approval and pairing counts surface as Open badges. Sessions 4-6 grow this set.
    /// </summary>
    private static TrayFlyoutSection BuildQuickActionsSection(WindowsTraySnapshot snapshot)
    {
        var pending = snapshot.PendingApprovalCount + snapshot.PendingPairingCount;
        var actionRows = new List<TrayActionRow>
        {
            new("Open OpenClaw", OpenGlyph, TrayFlyoutAction.OpenShell, pending > 0 ? pending.ToString() : null),
            new("Settings", SettingsGlyph, TrayFlyoutAction.OpenSettings),
            new("Logs", LogsGlyph, TrayFlyoutAction.OpenLogs),
        };

        return new TrayFlyoutSection(Heading: null, StatusRows: [], ActionRows: actionRows);
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
