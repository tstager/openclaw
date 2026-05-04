using OpenClaw.Windows;

namespace OpenClaw.Windows.Tests;

[TestClass]
public sealed class OperatorWorkflowSummaryTests
{
    [TestMethod]
    public void ConnectedWithoutPendingWorkShowsReadyStates()
    {
        var summary = OperatorWorkflowSummary.Create([], [], GatewayRealtimeState.Connected);

        Assert.AreEqual(0, summary.PendingApprovals);
        Assert.AreEqual(0, summary.PendingPairingRequests);
        Assert.AreEqual("No approvals pending", summary.ApprovalsStatus);
        Assert.AreEqual("Ready for pairing requests", summary.PairingStatus);
    }

    [TestMethod]
    public void PendingWorkShowsActionableCounts()
    {
        var approvals = new[]
        {
            new PendingApproval("approval-1", "pwsh -NoProfile", @"C:\repo", "main", "agent:main"),
            new PendingApproval("approval-2", "git status", null, null, null),
        };
        var requests = new[]
        {
            new PairingRequest("pair-1", "device", "Windows laptop", "device-1"),
        };

        var summary = OperatorWorkflowSummary.Create(approvals, requests, GatewayRealtimeState.Connected);

        Assert.AreEqual("2 approvals pending", summary.ApprovalsStatus);
        Assert.AreEqual("1 pairing request pending", summary.PairingStatus);
        Assert.AreEqual("Pairing action needed", summary.PairingReadiness);
    }

    [TestMethod]
    public void DisconnectedGatewayShowsPairingCannotBeChecked()
    {
        var summary = OperatorWorkflowSummary.Create([], [], GatewayRealtimeState.Disconnected);

        Assert.AreEqual("Connect to check pairing", summary.PairingStatus);
    }
}
