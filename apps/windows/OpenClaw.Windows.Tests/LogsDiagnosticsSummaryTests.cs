using OpenClaw.Windows;

namespace OpenClaw.Windows.Tests;

[TestClass]
public sealed class LogsDiagnosticsSummaryTests
{
    [TestMethod]
    public void CreateUsesGatewayStatusAndLastRefresh()
    {
        var status = new GatewayStatusSnapshot(
            State: "running",
            ServiceInstalled: true,
            Reachable: true,
            Capability: "admin_capable",
            DashboardUrl: "http://127.0.0.1:18080",
            LogPath: @"C:\openclaw\gateway.log",
            AuthWarning: null,
            Error: null,
            RawJson: "{}");

        var summary = LogsDiagnosticsSummary.Create(
            @"C:\openclaw\crash.log",
            status,
            null,
            DateTimeOffset.Parse("2026-05-04T20:30:00Z"));

        Assert.AreEqual(@"C:\openclaw\crash.log", summary.AppLogPath);
        Assert.AreEqual(@"C:\openclaw\gateway.log", summary.GatewayLogPath);
        Assert.AreEqual(@"C:\openclaw", summary.AppLogFolderPath);
        Assert.AreEqual(@"C:\openclaw", summary.GatewayLogFolderPath);
        Assert.IsTrue(summary.CanUseAppLogActions);
        Assert.IsTrue(summary.CanUseGatewayLogActions);
        Assert.AreEqual("running", summary.GatewayStatus);
        Assert.AreEqual("none", summary.LastError);
        Assert.AreNotEqual("never", summary.LastRefresh);
    }

    [TestMethod]
    public void CreateHandlesMissingGatewayStatus()
    {
        var summary = LogsDiagnosticsSummary.Create("", null, "Gateway unavailable.", null);

        Assert.AreEqual("unknown", summary.AppLogPath);
        Assert.AreEqual("unknown", summary.GatewayLogPath);
        Assert.AreEqual("unknown", summary.AppLogFolderPath);
        Assert.AreEqual("unknown", summary.GatewayLogFolderPath);
        Assert.IsFalse(summary.CanUseAppLogActions);
        Assert.IsFalse(summary.CanUseGatewayLogActions);
        Assert.AreEqual("unknown", summary.GatewayStatus);
        Assert.AreEqual("Gateway unavailable.", summary.LastError);
        Assert.AreEqual("never", summary.LastRefresh);
    }
}
