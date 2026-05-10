using OpenClaw.Windows;

namespace OpenClaw.Windows.Tests;

[TestClass]
public sealed class GatewayDashboardSummaryTests
{
    [TestMethod]
    public void CreatesOperationalSummaryFromGatewayRealtimeAndOnboardingState()
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
        var checks = new[]
        {
            new OnboardingCheckResult("openclaw", "OpenClaw CLI", OnboardingCheckState.Passed, "2026.5.4"),
            new OnboardingCheckResult("node", "Node runtime", OnboardingCheckState.Warning, "Node probe was slow."),
            new OnboardingCheckResult("gateway", "Gateway status", OnboardingCheckState.Failed, "Gateway probe failed."),
        };

        var summary = GatewayDashboardSummary.Create(status, GatewayRealtimeState.Connected, checks);

        Assert.AreEqual("running", summary.GatewayState);
        Assert.AreEqual("Installed", summary.ServiceState);
        Assert.AreEqual("Reachable", summary.Reachability);
        Assert.AreEqual("admin_capable", summary.Capability);
        Assert.AreEqual("Connected", summary.ConnectionState);
        Assert.AreEqual("1 passed, 1 warning, 1 failed", summary.OnboardingHealth);
        Assert.AreEqual("Needs attention", summary.HealthState);
    }

    [TestMethod]
    public void MarksUnreachableGatewayAsNeedingAttention()
    {
        var status = new GatewayStatusSnapshot(
            State: "stopped",
            ServiceInstalled: false,
            Reachable: false,
            Capability: "unknown",
            DashboardUrl: null,
            LogPath: null,
            AuthWarning: null,
            Error: "Gateway is not responding.",
            RawJson: "{}");

        var summary = GatewayDashboardSummary.Create(status, GatewayRealtimeState.Disconnected, []);

        Assert.AreEqual("Not installed", summary.ServiceState);
        Assert.AreEqual("Unreachable", summary.Reachability);
        Assert.AreEqual("0 passed, 0 warnings, 0 failed", summary.OnboardingHealth);
        Assert.AreEqual("Needs attention", summary.HealthState);
    }

    [TestMethod]
    public void MarksMissingGatewayStatusAsChecking()
    {
        var summary = GatewayDashboardSummary.Create(null, GatewayRealtimeState.Disconnected, []);

        Assert.AreEqual("unknown", summary.GatewayState);
        Assert.AreEqual("Unknown", summary.ServiceState);
        Assert.AreEqual("Unknown", summary.Reachability);
        Assert.AreEqual("Checking", summary.HealthState);
    }

    [TestMethod]
    public void UsesRealtimeAuthorizationForCapabilityWhenConnected()
    {
        var status = new GatewayStatusSnapshot(
            State: "running",
            ServiceInstalled: true,
            Reachable: true,
            Capability: "read_only",
            DashboardUrl: null,
            LogPath: null,
            AuthWarning: null,
            Error: null,
            RawJson: "{}");

        var summary = GatewayDashboardSummary.Create(
            status,
            GatewayRealtimeState.Connected,
            [],
            new GatewayRealtimeAuthorization("operator", ["operator.read", "operator.write"]));

        Assert.AreEqual("write_capable", summary.Capability);
    }
}
