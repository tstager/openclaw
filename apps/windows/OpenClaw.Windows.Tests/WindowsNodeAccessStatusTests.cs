using OpenClaw.Windows;

namespace OpenClaw.Windows.Tests;

[TestClass]
public sealed class WindowsNodeAccessStatusTests
{
    [TestMethod]
    public void ReportsMissingScopesAndRepairAvailabilityWhenNarrow()
    {
        var authorization = new GatewayRealtimeAuthorization(
            "operator",
            ["operator.read", "operator.write", "operator.approvals", "operator.pairing"]);

        var status = WindowsNodeAccessStatus.Create(
            authorization,
            GatewayRealtimeState.Connected,
            WindowsCanvasNodeState.Connected,
            systemExecutionAdvertised: false);

        Assert.IsTrue(status.OperatorConnected);
        Assert.IsTrue(status.NodeConnected);
        Assert.IsTrue(status.RepairAvailable);
        CollectionAssert.AreEqual(
            new[] { "operator.admin", "operator.talk.secrets" },
            status.MissingScopes.ToArray());
        Assert.IsFalse(status.RequiresAdminForNode);
        StringAssert.Contains(status.Explanation, "operator.pairing");
    }

    [TestMethod]
    public void ExplainsAdminRequirementWhenNodeAdvertisesSystemExecution()
    {
        var authorization = new GatewayRealtimeAuthorization(
            "operator",
            GatewayRealtimeClient.RequestedOperatorScopes.ToArray());

        var status = WindowsNodeAccessStatus.Create(
            authorization,
            GatewayRealtimeState.Connected,
            WindowsCanvasNodeState.Connected,
            systemExecutionAdvertised: true);

        Assert.IsTrue(status.RequiresAdminForNode);
        Assert.IsFalse(status.RepairAvailable);
        Assert.IsEmpty(status.MissingScopes);
        StringAssert.Contains(status.Explanation, "operator.admin");
    }

    [TestMethod]
    public void FallsBackWhenOperatorNotConnected()
    {
        var status = WindowsNodeAccessStatus.Create(
            authorization: null,
            GatewayRealtimeState.Disconnected,
            WindowsCanvasNodeState.Disconnected,
            systemExecutionAdvertised: false);

        Assert.IsFalse(status.OperatorConnected);
        Assert.IsFalse(status.RepairAvailable);
        Assert.AreEqual("unknown", status.Capability);
        CollectionAssert.AreEqual(
            GatewayRealtimeClient.RequestedOperatorScopes.ToArray(),
            status.MissingScopes.ToArray());
        Assert.AreEqual("Disconnected", status.NodeStateLabel);
    }
}
