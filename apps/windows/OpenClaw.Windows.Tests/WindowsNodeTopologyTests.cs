using OpenClaw.Windows;

namespace OpenClaw.Windows.Tests;

[TestClass]
public sealed class WindowsNodeTopologyTests
{
    private static GatewayStatusSnapshot RunningStatus() => new(
        State: "running",
        ServiceInstalled: true,
        Reachable: true,
        Capability: "admin_capable",
        DashboardUrl: "http://127.0.0.1:18080",
        LogPath: @"C:\openclaw\gateway.log",
        AuthWarning: null,
        Error: null,
        RawJson: "{}");

    private static WindowsTraySnapshot Snapshot(
        IReadOnlyList<GatewayNodeSummary>? nodes,
        string? localNodeId = null,
        WindowsCanvasNodeState canvas = WindowsCanvasNodeState.Connected,
        bool canvasEnabled = true)
    {
        return WindowsTraySnapshot.Create(
            RunningStatus(),
            GatewayRealtimeState.Connected,
            canvas,
            canvasEnabled,
            AppPreferences.Default,
            sessionCount: 0,
            pendingApprovalCount: 0,
            pendingPairingCount: 0,
            lastActivity: null,
            latestNotification: null,
            nodes: nodes,
            localNodeId: localNodeId);
    }

    [TestMethod]
    public void SnapshotMarksGatewayReportedLocalNode()
    {
        var snapshot = Snapshot(
            [
                new GatewayNodeSummary("win-1", "Trent PC", "windows", Connected: true, Paired: true),
                new GatewayNodeSummary("mac-1", "Mac mini", "macos", Connected: false, Paired: true),
            ],
            localNodeId: "win-1");

        Assert.HasCount(2, snapshot.Nodes);
        Assert.IsTrue(snapshot.Nodes[0].IsLocal);
        Assert.AreEqual("This Windows node", snapshot.Nodes[0].Name);
        Assert.IsFalse(snapshot.Nodes[1].IsLocal);
        Assert.AreEqual(1, snapshot.ConnectedNodeCount);
        Assert.AreEqual(2, snapshot.PairedNodeCount);
    }

    [TestMethod]
    public void SnapshotSynthesizesLocalNodeWhenGatewayHasNotReportedIt()
    {
        var snapshot = Snapshot(
            [new GatewayNodeSummary("mac-1", "Mac mini", "macos", Connected: true, Paired: true)],
            localNodeId: "win-1",
            canvas: WindowsCanvasNodeState.Connected);

        Assert.HasCount(2, snapshot.Nodes);
        Assert.IsTrue(snapshot.Nodes[0].IsLocal);
        Assert.AreEqual("windows", snapshot.Nodes[0].Platform);
        Assert.IsTrue(snapshot.Nodes[0].Online);
    }

    [TestMethod]
    public void SnapshotOmitsLocalNodeWhenCanvasDisabled()
    {
        var snapshot = Snapshot([], localNodeId: "win-1", canvas: WindowsCanvasNodeState.Disconnected, canvasEnabled: false);

        Assert.IsEmpty(snapshot.Nodes);
    }

    [TestMethod]
    public void ComposerRendersPerNodeRowsWithPlatformBadge()
    {
        var snapshot = Snapshot(
            [
                new GatewayNodeSummary("win-1", "Trent PC", "windows", Connected: true, Paired: true),
                new GatewayNodeSummary("ios-1", "iPhone", "ios", Connected: false, Paired: true),
            ],
            localNodeId: "win-1");

        var model = TrayFlyoutComposer.Compose(snapshot);
        var nodesSection = model.Sections.Single(section => section.Heading == "Nodes");

        Assert.HasCount(2, nodesSection.StatusRows);
        Assert.AreEqual("This Windows node", nodesSection.StatusRows[0].Label);
        Assert.AreEqual("windows", nodesSection.StatusRows[0].Badge);
        StringAssert.Contains(nodesSection.StatusRows[0].Detail!, "Online");
        Assert.AreEqual("ios", nodesSection.StatusRows[1].Badge);
        StringAssert.Contains(nodesSection.StatusRows[1].Detail!, "Offline");
        Assert.AreEqual(TrayFlyoutAction.OpenPairing, nodesSection.StatusRows[0].Action);
    }

    [TestMethod]
    public void ComposerAddsNodeCountToGatewayRow()
    {
        var snapshot = Snapshot(
            [new GatewayNodeSummary("win-1", "Trent PC", "windows", Connected: true, Paired: true)],
            localNodeId: "win-1");

        var model = TrayFlyoutComposer.Compose(snapshot);
        var gatewayRow = model.Sections[0].StatusRows[0];

        StringAssert.StartsWith(gatewayRow.Label, "Gateway:");
        StringAssert.Contains(gatewayRow.Detail!, "node(s) online");
        StringAssert.Contains(gatewayRow.Detail!, "paired");
    }

    [TestMethod]
    public void ComposerOmitsNodesSectionWhenNoNodes()
    {
        var snapshot = Snapshot([], localNodeId: null, canvas: WindowsCanvasNodeState.Disconnected, canvasEnabled: false);

        var model = TrayFlyoutComposer.Compose(snapshot);

        Assert.IsFalse(model.Sections.Any(section => section.Heading == "Nodes"));
    }

    [TestMethod]
    public void ComposerWiresHeaderMasterToggleToNodeEnablement()
    {
        var enabled = TrayFlyoutComposer.Compose(Snapshot([], canvasEnabled: true));
        Assert.IsNotNull(enabled.Header);
        Assert.IsTrue(enabled.Header!.NodeEnabled);
        Assert.AreEqual(TrayFlyoutAction.ToggleCanvasNode, enabled.Header.MasterToggleAction);

        var disabled = TrayFlyoutComposer.Compose(
            Snapshot([], canvas: WindowsCanvasNodeState.Disconnected, canvasEnabled: false));
        Assert.IsFalse(disabled.Header!.NodeEnabled);
    }
}
