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
    OpenNotificationHistory,
    OpenActivityHistory,
    OpenSupportSummary,
    OpenCrashLog,
    OpenAppLogFolder,
    OpenGatewayLogFolder,
    CreateSupportArtifact,
    OpenQuickSend,
    OpenReconfigure,
    OpenAbout,
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
/// A status-dot row: a small colored dot, a label, and an optional right-aligned badge. When <paramref name="Action"/>
/// is set, the row becomes a navigation entry point that shows a trailing chevron and dismisses-then-navigates;
/// when it is null the row is display-only and non-interactive.
/// </summary>
public sealed record TrayStatusRow(string Label, string? Detail, TrayStatusTone Tone, string? Badge, TrayFlyoutAction? Action = null);

/// <summary>
/// An icon-plus-label action row, optionally carrying a right-aligned badge (e.g. a pending count) or a
/// right-aligned keyboard-accelerator hint (e.g. <c>Ctrl+Alt+;</c>) for rows backed by a real shortcut.
/// </summary>
public sealed record TrayActionRow(string Label, string Glyph, TrayFlyoutAction Action, string? Badge = null, string? Accelerator = null);

/// <summary>
/// An icon-plus-label permission toggle row. Activating it flips a preference-backed capability without
/// dismissing the flyout, so it routes through the flyout's toggle channel rather than the action channel.
/// </summary>
public sealed record TrayToggleRow(string Label, string Glyph, bool IsOn, TrayFlyoutAction ToggleAction);

/// <summary>
/// A per-node topology row: a node's display name, an online/paired role line, and a platform badge.
/// </summary>
public sealed record TrayNodeRow(string Name, string? Platform, bool Online, bool Paired, bool IsLocal);

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
/// The branded header band shown above the first section: the app mark glyph, the product title, and the
/// node master toggle (the top-right switch that enables/disables the Windows node).
/// </summary>
public sealed record TrayFlyoutHeader(
    string Title,
    string IconGlyph,
    bool NodeEnabled = false,
    TrayFlyoutAction? MasterToggleAction = null);

/// <summary>
/// The compact, ordered set of sections the tray flyout renders for a given snapshot, with an optional
/// branded header band rendered above them.
/// </summary>
public sealed record TrayFlyoutModel(IReadOnlyList<TrayFlyoutSection> Sections, TrayFlyoutHeader? Header = null);

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
    private const string HistoryGlyph = "";
    private const string ActivityGlyph = "";
    private const string SupportGlyph = "";
    private const string CrashLogGlyph = "";
    private const string FolderGlyph = "";
    private const string GatewayFolderGlyph = "";
    private const string ArtifactGlyph = "";

    // Segoe Fluent Icons glyphs for the reference's added quick actions: Send, Repair, and Info.
    private const string QuickSendGlyph = "";
    private const string ReconfigureGlyph = "";
    private const string AboutGlyph = "";

    /// <summary>
    /// The product title shown in the flyout header band and the app mark glyph (lobster) beside it. There is no
    /// bundled lobster asset, so the emoji is the brand mark, matching the reference flyout.
    /// </summary>
    private const string HeaderTitle = "OpenClaw";
    private const string HeaderIconGlyph = "🦞";

    /// <summary>
    /// The single source of truth for the Companion Settings accelerator hint. <c>MainWindow</c> registers the
    /// matching real Ctrl+Alt+; window accelerator so the displayed hint is truthful.
    /// </summary>
    public const string CompanionSettingsAccelerator = "Ctrl+Alt+;";

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
        };
        if (snapshot.Nodes.Count > 0)
        {
            sections.Add(BuildNodesSection(snapshot));
        }
        sections.Add(BuildQuickActionsSection(snapshot));
        sections.Add(BuildPermissionsSection(snapshot));
        sections.Add(BuildSupportSection());
        sections.Add(BuildGatewaySection(snapshot));

        return new TrayFlyoutModel(
            sections,
            new TrayFlyoutHeader(
                HeaderTitle,
                HeaderIconGlyph,
                snapshot.CanvasNodeEnabled,
                TrayFlyoutAction.ToggleCanvasNode));
    }

    /// <summary>
    /// Per-node topology rows (this Windows node and any remote nodes the gateway reports), each with an
    /// online/paired role line and a platform badge, mirroring the reference flyout's node list.
    /// </summary>
    private static TrayFlyoutSection BuildNodesSection(WindowsTraySnapshot snapshot)
    {
        var statusRows = snapshot.Nodes
            .Select(node => new TrayStatusRow(
                Label: node.Name,
                Detail: DescribeNodeRole(node),
                Tone: node.Online ? TrayStatusTone.Success : node.Paired ? TrayStatusTone.Caution : TrayStatusTone.Neutral,
                Badge: DescribePlatform(node.Platform),
                Action: TrayFlyoutAction.OpenPairing))
            .ToArray();

        return new TrayFlyoutSection(Heading: "Nodes", StatusRows: statusRows, ActionRows: []);
    }

    private static string DescribeNodeRole(TrayNodeRow node)
    {
        var presence = node.Online ? "Online" : "Offline";
        var pairing = node.Paired ? "paired" : "unpaired";
        return $"{presence} · {pairing} node";
    }

    private static string DescribePlatform(string? platform)
    {
        return string.IsNullOrWhiteSpace(platform) ? "node" : platform;
    }

    /// <summary>
    /// Live status-dot rows for the gateway, Canvas/A2UI node, sessions, and the latest activity. The gateway,
    /// Canvas, and Sessions rows carry a navigation action so they render a chevron and act as entry points;
    /// the Activity row stays display-only.
    /// </summary>
    private static TrayFlyoutSection BuildStatusSection(WindowsTraySnapshot snapshot)
    {
        var statusRows = new List<TrayStatusRow>
        {
            new(
                Label: $"Gateway: {DescribeGateway(snapshot)}",
                Detail: DescribeGatewayDetail(snapshot),
                Tone: ResolveGatewayTone(snapshot),
                Badge: snapshot.GatewayIsLocal ? "Local" : "Remote",
                Action: TrayFlyoutAction.OpenHome),
            new(
                Label: $"Canvas: {DescribeCanvas(snapshot.CanvasReadiness)}",
                Detail: null,
                Tone: ResolveCanvasTone(snapshot.CanvasReadiness),
                Badge: null,
                Action: TrayFlyoutAction.OpenCanvas),
        };

        if (snapshot.SessionCount > 0)
        {
            statusRows.Add(new TrayStatusRow(
                Label: $"Sessions: {snapshot.SessionCount}",
                Detail: null,
                Tone: TrayStatusTone.Accent,
                Badge: snapshot.SessionCount.ToString(),
                Action: TrayFlyoutAction.OpenSessions));
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
    /// Always-available navigation quick-actions, ordered to match the reference flyout. "Dashboard" is the home
    /// page; Quick Send and Reconfigure sit next to Canvas; "Companion Settings…" carries its real Ctrl+Alt+;
    /// accelerator hint and is followed by About. Approvals, Pairing, and Sessions carry their own right-aligned
    /// count badges (Sessions only when &gt; 0). Sessions 5-6 grow this set.
    /// </summary>
    private static TrayFlyoutSection BuildQuickActionsSection(WindowsTraySnapshot snapshot)
    {
        var actionRows = new List<TrayActionRow>
        {
            new("Open OpenClaw", OpenGlyph, TrayFlyoutAction.OpenShell),
            new("Dashboard", HomeGlyph, TrayFlyoutAction.OpenHome),
            new("Chat", ChatGlyph, TrayFlyoutAction.OpenChat),
            new("Canvas", CanvasGlyph, TrayFlyoutAction.OpenCanvas),
            new("Quick Send…", QuickSendGlyph, TrayFlyoutAction.OpenQuickSend),
            new("Reconfigure…", ReconfigureGlyph, TrayFlyoutAction.OpenReconfigure),
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
            new("Companion Settings…", SettingsGlyph, TrayFlyoutAction.OpenSettings, Badge: null, Accelerator: CompanionSettingsAccelerator),
            new("About", AboutGlyph, TrayFlyoutAction.OpenAbout),
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
    /// Diagnostics and support entry points that reuse the existing stores: notification and activity history
    /// store files, the support-summary view on the Logs page, the crash log file, the companion data folder,
    /// the gateway log folder, and on-demand support-artifact creation. Every row dismisses the flyout through
    /// the action channel because each one opens a file/folder or navigates the shell.
    /// </summary>
    private static TrayFlyoutSection BuildSupportSection()
    {
        var actionRows = new List<TrayActionRow>
        {
            new("Notification history", HistoryGlyph, TrayFlyoutAction.OpenNotificationHistory),
            new("Activity history", ActivityGlyph, TrayFlyoutAction.OpenActivityHistory),
            new("Support summary", SupportGlyph, TrayFlyoutAction.OpenSupportSummary),
            new("Crash log", CrashLogGlyph, TrayFlyoutAction.OpenCrashLog),
            new("App log folder", FolderGlyph, TrayFlyoutAction.OpenAppLogFolder),
            new("Gateway log folder", GatewayFolderGlyph, TrayFlyoutAction.OpenGatewayLogFolder),
            new("Create support artifact", ArtifactGlyph, TrayFlyoutAction.CreateSupportArtifact),
        };

        return new TrayFlyoutSection(Heading: "Support", StatusRows: [], ActionRows: actionRows);
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

    /// <summary>
    /// Annotates the gateway row's detail with the node-paired / connected-node summary when any nodes are known.
    /// </summary>
    private static string DescribeGatewayDetail(WindowsTraySnapshot snapshot)
    {
        if (snapshot.Nodes.Count == 0)
        {
            return snapshot.GatewayUrl;
        }
        return $"{snapshot.GatewayUrl} · {snapshot.ConnectedNodeCount} node(s) online, {snapshot.PairedNodeCount} paired";
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
