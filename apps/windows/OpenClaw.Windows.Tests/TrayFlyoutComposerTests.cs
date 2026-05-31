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
        int pairings = 0)
    {
        return WindowsTraySnapshot.Create(
            status,
            realtime,
            canvas,
            canvasEnabled,
            AppPreferences.Default,
            sessionCount: 0,
            pendingApprovalCount: approvals,
            pendingPairingCount: pairings,
            lastActivity: null,
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
    public void PendingApprovalsAndPairingsBadgeTheOpenAction()
    {
        var model = TrayFlyoutComposer.Compose(
            Snapshot(RunningStatus(), GatewayRealtimeState.Connected, WindowsCanvasNodeState.Connected, approvals: 2, pairings: 1));

        var openRow = AllActionRows(model).First(row => row.Action == TrayFlyoutAction.OpenShell);
        Assert.AreEqual("3", openRow.Badge);
    }

    [TestMethod]
    public void NoPendingWorkLeavesOpenActionWithoutBadge()
    {
        var model = TrayFlyoutComposer.Compose(
            Snapshot(RunningStatus(), GatewayRealtimeState.Connected, WindowsCanvasNodeState.Connected));

        var openRow = AllActionRows(model).First(row => row.Action == TrayFlyoutAction.OpenShell);
        Assert.IsNull(openRow.Badge);
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
