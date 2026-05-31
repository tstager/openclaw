using OpenClaw.Windows;

namespace OpenClaw.Windows.Tests;

[TestClass]
public sealed class TrayFlyoutComposerTests
{
    private static GatewayStatusSnapshot RunningStatus(bool serviceInstalled = true)
    {
        return new GatewayStatusSnapshot(
            State: "running",
            ServiceInstalled: serviceInstalled,
            Reachable: true,
            Capability: "admin_capable",
            DashboardUrl: "http://127.0.0.1:18080",
            LogPath: @"C:\openclaw\gateway.log",
            AuthWarning: null,
            Error: null,
            RawJson: "{}");
    }

    private static GatewayStatusSnapshot StoppedStatus(bool serviceInstalled = true)
    {
        return new GatewayStatusSnapshot(
            State: "stopped",
            ServiceInstalled: serviceInstalled,
            Reachable: false,
            Capability: "unknown",
            DashboardUrl: null,
            LogPath: null,
            AuthWarning: null,
            Error: null,
            RawJson: "{}");
    }

    private static WindowsTraySnapshot Snapshot(
        GatewayStatusSnapshot? status,
        GatewayRealtimeState realtime,
        WindowsCanvasNodeState canvas,
        bool canvasEnabled = true,
        int approvals = 0,
        int pairings = 0,
        int sessions = 0,
        string? lastActivity = null)
    {
        return WindowsTraySnapshot.Create(
            status,
            realtime,
            canvas,
            canvasEnabled,
            AppPreferences.Default,
            sessionCount: sessions,
            pendingApprovalCount: approvals,
            pendingPairingCount: pairings,
            lastActivity: lastActivity,
            latestNotification: null);
    }

    private static IEnumerable<TrayActionRow> AllActionRows(TrayFlyoutModel model)
    {
        return model.Sections.SelectMany(section => section.ActionRows);
    }

    private static IEnumerable<TrayStatusRow> AllStatusRows(TrayFlyoutModel model)
    {
        return model.Sections.SelectMany(section => section.StatusRows);
    }

    [TestMethod]
    public void ComposesGatewayCanvasStatusRowsAndCoreActions()
    {
        var model = TrayFlyoutComposer.Compose(
            Snapshot(RunningStatus(), GatewayRealtimeState.Connected, WindowsCanvasNodeState.Connected));

        var statusLabels = AllStatusRows(model).Select(row => row.Label).ToArray();
        Assert.IsTrue(statusLabels.Any(label => label.StartsWith("Gateway:", StringComparison.Ordinal)));
        Assert.IsTrue(statusLabels.Any(label => label.StartsWith("Canvas:", StringComparison.Ordinal)));

        var actions = AllActionRows(model).Select(row => row.Action).ToArray();
        CollectionAssert.Contains(actions, TrayFlyoutAction.OpenShell);
        CollectionAssert.Contains(actions, TrayFlyoutAction.OpenSettings);
        CollectionAssert.Contains(actions, TrayFlyoutAction.OpenLogs);
        CollectionAssert.Contains(actions, TrayFlyoutAction.Exit);
    }

    [TestMethod]
    public void ExitIsAlwaysTheLastAction()
    {
        var model = TrayFlyoutComposer.Compose(
            Snapshot(StoppedStatus(), GatewayRealtimeState.Disconnected, WindowsCanvasNodeState.Disconnected));

        var actions = AllActionRows(model).ToArray();
        Assert.AreEqual(TrayFlyoutAction.Exit, actions[^1].Action);
    }

    [TestMethod]
    public void RunningConnectedGatewayDotIsSuccess()
    {
        var model = TrayFlyoutComposer.Compose(
            Snapshot(RunningStatus(), GatewayRealtimeState.Connected, WindowsCanvasNodeState.Connected));

        var gatewayRow = AllStatusRows(model).First(row => row.Label.StartsWith("Gateway:", StringComparison.Ordinal));
        Assert.AreEqual(TrayStatusTone.Success, gatewayRow.Tone);
    }

    [TestMethod]
    public void RunningButDisconnectedGatewayDotIsCaution()
    {
        var model = TrayFlyoutComposer.Compose(
            Snapshot(RunningStatus(), GatewayRealtimeState.Disconnected, WindowsCanvasNodeState.Disconnected));

        var gatewayRow = AllStatusRows(model).First(row => row.Label.StartsWith("Gateway:", StringComparison.Ordinal));
        Assert.AreEqual(TrayStatusTone.Caution, gatewayRow.Tone);
    }

    [TestMethod]
    public void StoppedGatewayDotIsCritical()
    {
        var model = TrayFlyoutComposer.Compose(
            Snapshot(StoppedStatus(), GatewayRealtimeState.Disconnected, WindowsCanvasNodeState.Disconnected));

        var gatewayRow = AllStatusRows(model).First(row => row.Label.StartsWith("Gateway:", StringComparison.Ordinal));
        Assert.AreEqual(TrayStatusTone.Critical, gatewayRow.Tone);
    }

    [TestMethod]
    public void ReadyCanvasDotIsSuccessAndDisabledIsNeutral()
    {
        var ready = TrayFlyoutComposer.Compose(
            Snapshot(RunningStatus(), GatewayRealtimeState.Connected, WindowsCanvasNodeState.Connected));
        var disabled = TrayFlyoutComposer.Compose(
            Snapshot(RunningStatus(), GatewayRealtimeState.Connected, WindowsCanvasNodeState.Connected, canvasEnabled: false));

        Assert.AreEqual(
            TrayStatusTone.Success,
            AllStatusRows(ready).First(row => row.Label.StartsWith("Canvas:", StringComparison.Ordinal)).Tone);
        Assert.AreEqual(
            TrayStatusTone.Neutral,
            AllStatusRows(disabled).First(row => row.Label.StartsWith("Canvas:", StringComparison.Ordinal)).Tone);
    }

    [TestMethod]
    public void DisconnectedRealtimeOffersConnectAction()
    {
        var model = TrayFlyoutComposer.Compose(
            Snapshot(StoppedStatus(), GatewayRealtimeState.Disconnected, WindowsCanvasNodeState.Disconnected));

        var actions = AllActionRows(model).Select(row => row.Action).ToArray();
        CollectionAssert.Contains(actions, TrayFlyoutAction.ConnectRealtime);
        CollectionAssert.DoesNotContain(actions, TrayFlyoutAction.DisconnectRealtime);
    }

    [TestMethod]
    public void ConnectedRealtimeOffersDisconnectAction()
    {
        var model = TrayFlyoutComposer.Compose(
            Snapshot(RunningStatus(), GatewayRealtimeState.Connected, WindowsCanvasNodeState.Connected));

        var actions = AllActionRows(model).Select(row => row.Action).ToArray();
        CollectionAssert.Contains(actions, TrayFlyoutAction.DisconnectRealtime);
        CollectionAssert.DoesNotContain(actions, TrayFlyoutAction.ConnectRealtime);
    }

    [TestMethod]
    public void ConnectingRealtimeOffersNoRealtimeToggle()
    {
        var model = TrayFlyoutComposer.Compose(
            Snapshot(RunningStatus(), GatewayRealtimeState.Connecting, WindowsCanvasNodeState.Connecting));

        var actions = AllActionRows(model).Select(row => row.Action).ToArray();
        CollectionAssert.DoesNotContain(actions, TrayFlyoutAction.ConnectRealtime);
        CollectionAssert.DoesNotContain(actions, TrayFlyoutAction.DisconnectRealtime);
    }

    [TestMethod]
    public void RunningGatewayExposesRestartAndStopRows()
    {
        var model = TrayFlyoutComposer.Compose(
            Snapshot(RunningStatus(), GatewayRealtimeState.Connected, WindowsCanvasNodeState.Connected));

        var actions = AllActionRows(model).Select(row => row.Action).ToArray();
        CollectionAssert.Contains(actions, TrayFlyoutAction.RunGatewayRestart);
        CollectionAssert.Contains(actions, TrayFlyoutAction.RunGatewayStop);
        CollectionAssert.DoesNotContain(actions, TrayFlyoutAction.RunGatewayStart);
    }

    [TestMethod]
    public void UninstalledStoppedGatewayExposesInstallAndStartRows()
    {
        var model = TrayFlyoutComposer.Compose(
            Snapshot(StoppedStatus(serviceInstalled: false), GatewayRealtimeState.Disconnected, WindowsCanvasNodeState.Disconnected));

        var actions = AllActionRows(model).Select(row => row.Action).ToArray();
        CollectionAssert.Contains(actions, TrayFlyoutAction.RunGatewayInstall);
        CollectionAssert.Contains(actions, TrayFlyoutAction.RunGatewayStart);
    }

    [TestMethod]
    public void OpenShellRowNeverCarriesAPendingBadge()
    {
        var model = TrayFlyoutComposer.Compose(
            Snapshot(RunningStatus(), GatewayRealtimeState.Connected, WindowsCanvasNodeState.Connected, approvals: 2, pairings: 1));

        var openRow = AllActionRows(model).First(row => row.Action == TrayFlyoutAction.OpenShell);
        Assert.IsNull(openRow.Badge);
    }

    [TestMethod]
    public void NavigationQuickActionsArePresentWithGlyphsAndActions()
    {
        var model = TrayFlyoutComposer.Compose(
            Snapshot(RunningStatus(), GatewayRealtimeState.Connected, WindowsCanvasNodeState.Connected));

        var byAction = AllActionRows(model).ToDictionary(row => row.Action);
        foreach (var action in new[]
        {
            TrayFlyoutAction.OpenHome,
            TrayFlyoutAction.OpenChat,
            TrayFlyoutAction.OpenCanvas,
            TrayFlyoutAction.OpenSessions,
            TrayFlyoutAction.OpenApprovals,
            TrayFlyoutAction.OpenPairing,
            TrayFlyoutAction.OpenSettings,
            TrayFlyoutAction.OpenLogs,
        })
        {
            Assert.IsTrue(byAction.ContainsKey(action), $"Missing quick-action row {action}.");
            Assert.IsFalse(string.IsNullOrEmpty(byAction[action].Glyph), $"Action {action} is missing a glyph.");
        }
    }

    [TestMethod]
    public void ApprovalsAndPairingRowsCarryTheirOwnCountBadges()
    {
        var model = TrayFlyoutComposer.Compose(
            Snapshot(RunningStatus(), GatewayRealtimeState.Connected, WindowsCanvasNodeState.Connected, approvals: 2, pairings: 1));

        var approvalsRow = AllActionRows(model).First(row => row.Action == TrayFlyoutAction.OpenApprovals);
        var pairingRow = AllActionRows(model).First(row => row.Action == TrayFlyoutAction.OpenPairing);
        Assert.AreEqual("2", approvalsRow.Badge);
        Assert.AreEqual("1", pairingRow.Badge);
    }

    [TestMethod]
    public void SessionsRowBadgesTheCountAndIsAbsentAtZero()
    {
        var withSessions = TrayFlyoutComposer.Compose(
            Snapshot(RunningStatus(), GatewayRealtimeState.Connected, WindowsCanvasNodeState.Connected, sessions: 5));
        var sessionsRow = AllActionRows(withSessions).First(row => row.Action == TrayFlyoutAction.OpenSessions);
        Assert.AreEqual("5", sessionsRow.Badge);

        var withoutSessions = TrayFlyoutComposer.Compose(
            Snapshot(RunningStatus(), GatewayRealtimeState.Connected, WindowsCanvasNodeState.Connected, sessions: 0));
        var zeroRow = AllActionRows(withoutSessions).First(row => row.Action == TrayFlyoutAction.OpenSessions);
        Assert.IsNull(zeroRow.Badge);
    }

    [TestMethod]
    public void ZeroPendingWorkLeavesApprovalsAndPairingRowsWithoutBadges()
    {
        var model = TrayFlyoutComposer.Compose(
            Snapshot(RunningStatus(), GatewayRealtimeState.Connected, WindowsCanvasNodeState.Connected));

        Assert.IsNull(AllActionRows(model).First(row => row.Action == TrayFlyoutAction.OpenApprovals).Badge);
        Assert.IsNull(AllActionRows(model).First(row => row.Action == TrayFlyoutAction.OpenPairing).Badge);
    }

    [TestMethod]
    public void QuickActionsFollowTheExpectedNavigationOrdering()
    {
        var model = TrayFlyoutComposer.Compose(
            Snapshot(StoppedStatus(serviceInstalled: false), GatewayRealtimeState.Disconnected, WindowsCanvasNodeState.Disconnected));

        var actions = AllActionRows(model).Select(row => row.Action).ToArray();
        var expectedNav = new[]
        {
            TrayFlyoutAction.OpenShell,
            TrayFlyoutAction.OpenHome,
            TrayFlyoutAction.OpenChat,
            TrayFlyoutAction.OpenCanvas,
            TrayFlyoutAction.OpenSessions,
            TrayFlyoutAction.OpenApprovals,
            TrayFlyoutAction.OpenPairing,
            TrayFlyoutAction.OpenSettings,
            TrayFlyoutAction.OpenLogs,
        };
        CollectionAssert.AreEqual(expectedNav, actions.Take(expectedNav.Length).ToArray());
        Assert.AreEqual(TrayFlyoutAction.Exit, actions[^1]);
    }

    [TestMethod]
    public void LocalRunningGatewayRowCarriesLocalBadge()
    {
        var model = TrayFlyoutComposer.Compose(
            Snapshot(RunningStatus(), GatewayRealtimeState.Connected, WindowsCanvasNodeState.Connected));

        var gatewayRow = AllStatusRows(model).First(row => row.Label.StartsWith("Gateway:", StringComparison.Ordinal));
        Assert.AreEqual("Local", gatewayRow.Badge);
    }

    [TestMethod]
    public void RemoteGatewayRowCarriesRemoteBadge()
    {
        var remoteStatus = new GatewayStatusSnapshot(
            State: "running",
            ServiceInstalled: true,
            Reachable: true,
            Capability: "admin_capable",
            DashboardUrl: "https://gateway.example.com:18080",
            LogPath: null,
            AuthWarning: null,
            Error: null,
            RawJson: "{}");
        var model = TrayFlyoutComposer.Compose(
            Snapshot(remoteStatus, GatewayRealtimeState.Connected, WindowsCanvasNodeState.Connected));

        var gatewayRow = AllStatusRows(model).First(row => row.Label.StartsWith("Gateway:", StringComparison.Ordinal));
        Assert.AreEqual("Remote", gatewayRow.Badge);
    }

    [TestMethod]
    public void KnownSessionsAddAnAccentSessionsRowWithBadge()
    {
        var model = TrayFlyoutComposer.Compose(
            Snapshot(RunningStatus(), GatewayRealtimeState.Connected, WindowsCanvasNodeState.Connected, sessions: 4));

        var sessionRow = AllStatusRows(model).First(row => row.Label.StartsWith("Sessions:", StringComparison.Ordinal));
        Assert.AreEqual(TrayStatusTone.Accent, sessionRow.Tone);
        Assert.AreEqual("4", sessionRow.Badge);
    }

    [TestMethod]
    public void ZeroSessionsOmitsTheSessionsRow()
    {
        var model = TrayFlyoutComposer.Compose(
            Snapshot(RunningStatus(), GatewayRealtimeState.Connected, WindowsCanvasNodeState.Connected, sessions: 0));

        Assert.IsFalse(AllStatusRows(model).Any(row => row.Label.StartsWith("Sessions:", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void PresentActivityAddsAnActivityRow()
    {
        var model = TrayFlyoutComposer.Compose(
            Snapshot(RunningStatus(), GatewayRealtimeState.Connected, WindowsCanvasNodeState.Connected, lastActivity: "Restart completed."));

        var activityRow = AllStatusRows(model).First(row => row.Label == "Activity");
        Assert.AreEqual("Restart completed.", activityRow.Detail);
    }

    [TestMethod]
    public void AbsentActivityOmitsTheActivityRow()
    {
        var model = TrayFlyoutComposer.Compose(
            Snapshot(RunningStatus(), GatewayRealtimeState.Connected, WindowsCanvasNodeState.Connected, lastActivity: null));

        Assert.IsFalse(AllStatusRows(model).Any(row => row.Label == "Activity"));
    }

    [TestMethod]
    public void TooltipIncludesGatewayCanvasLocalityAndStaysWithinLimit()
    {
        var tooltip = TrayFlyoutComposer.BuildTooltip(
            Snapshot(RunningStatus(), GatewayRealtimeState.Connected, WindowsCanvasNodeState.Connected),
            warningCount: 0);

        StringAssert.Contains(tooltip, "Running");
        StringAssert.Contains(tooltip, "Local");
        StringAssert.Contains(tooltip, "Canvas Ready");
        Assert.IsLessThanOrEqualTo(TrayFlyoutComposer.TooltipMaxLength, tooltip.Length);
    }

    [TestMethod]
    public void TooltipIncludesWarningCountWhenPresent()
    {
        var tooltip = TrayFlyoutComposer.BuildTooltip(
            Snapshot(RunningStatus(), GatewayRealtimeState.Connected, WindowsCanvasNodeState.Connected),
            warningCount: 3);

        StringAssert.Contains(tooltip, "3 warnings");
        Assert.IsLessThanOrEqualTo(TrayFlyoutComposer.TooltipMaxLength, tooltip.Length);
    }

    [TestMethod]
    public void TooltipTruncatesLongActivityToStayWithinLimit()
    {
        var tooltip = TrayFlyoutComposer.BuildTooltip(
            Snapshot(
                RunningStatus(),
                GatewayRealtimeState.Connected,
                WindowsCanvasNodeState.Connected,
                lastActivity: new string('x', 200)),
            warningCount: 0);

        Assert.AreEqual(TrayFlyoutComposer.TooltipMaxLength, tooltip.Length);
    }

    [TestMethod]
    public void EveryActionRowCarriesAGlyph()
    {
        var model = TrayFlyoutComposer.Compose(
            Snapshot(StoppedStatus(serviceInstalled: false), GatewayRealtimeState.Disconnected, WindowsCanvasNodeState.PairingRequired));

        foreach (var row in AllActionRows(model))
        {
            Assert.IsFalse(string.IsNullOrEmpty(row.Glyph), $"Action {row.Action} is missing a glyph.");
        }
    }
}
