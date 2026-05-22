using OpenClaw.Windows;
using OpenClaw.Windows.Native;

namespace OpenClaw.Windows.Tests;

[TestClass]
public sealed class WindowsCompanionCoordinatorTests
{
    [TestMethod]
    public async Task GatewayActionUpdatesReusableStatusAndActivityState()
    {
        var runner = new FakeGatewayCliCommandRunner(
            new GatewayCliResult(0, """{"ok":true}""", ""),
            new GatewayCliResult(0, """{"ok":true,"service":{"installed":true,"state":"running"},"rpc":{"ok":true,"capability":"admin_capable"},"logs":{"file":"C:\\openclaw.log"}}""", ""));
        var coordinator = CreateCoordinator(runner);

        var result = await coordinator.RunGatewayActionAsync(GatewayCliAction.Restart);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual("running", coordinator.GatewayStatus?.State);
        Assert.AreEqual(@"C:\openclaw.log", coordinator.LogPath);
        Assert.AreEqual("Restart completed.", coordinator.LastActivity);
        Assert.AreEqual("running", coordinator.DashboardSummary.GatewayState);
        CollectionAssert.AreEqual(new[] { "gateway", "restart", "--json" }, runner.Calls[0].ToArray());
        CollectionAssert.AreEqual(new[] { "gateway", "status", "--json" }, runner.Calls[1].ToArray());
    }

    [TestMethod]
    public async Task GatewayActionFailureKeepsReusableErrorState()
    {
        var runner = new FakeGatewayCliCommandRunner(
            new GatewayCliResult(1, "", "OpenClaw CLI was not found."));
        var coordinator = CreateCoordinator(runner);

        var result = await coordinator.RunGatewayActionAsync(GatewayCliAction.Start);

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual("unavailable", coordinator.GatewayStatus?.State);
        StringAssert.Contains(coordinator.LastActivity, "Start failed");
        StringAssert.Contains(coordinator.LastError, "OpenClaw CLI was not found");
    }

    [TestMethod]
    public void RealtimeStateAndEventsUpdateReusableState()
    {
        var coordinator = CreateCoordinator(new FakeGatewayCliCommandRunner());

        coordinator.ApplyRealtimeState(GatewayRealtimeState.AuthFailed, "Token rejected.");
        coordinator.RecordRealtimeEvent(new GatewayRealtimeEvent("approval.requested", null));

        Assert.AreEqual(GatewayRealtimeState.AuthFailed, coordinator.RealtimeState);
        Assert.AreEqual("Token rejected.", coordinator.RealtimeReason);
        Assert.AreEqual("Token rejected.", coordinator.LastError);
        Assert.AreEqual("Latest event: approval.requested", coordinator.LastActivity);
    }

    private static WindowsCompanionCoordinator CreateCoordinator(FakeGatewayCliCommandRunner runner)
    {
        var store = new AppPreferencesStore(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "preferences.json"));
        var gateway = new GatewayCompanionController(runner, store);
        var root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var state = new WindowsCompanionState
        {
            Summary = AppBootstrap.CreateStartupSummary(),
            Gateway = gateway,
            Realtime = new GatewayRealtimeClient(store),
            CanvasNode = new WindowsCanvasNodeClient(store, new DeviceIdentityStore(new InMemoryAppCredentialStore())),
            DeviceCapabilities = new WindowsDeviceCapabilityService(),
            OnboardingChecks = new OnboardingCheckService(runner, store),
            Preferences = store,
            Navigation = new WindowsNavigationService(),
            Notifications = new WindowsNotificationActivityLog(),
            Activation = new WindowsActivationRelay("test-activation"),
            Tunnel = new WindowsSshTunnelService(),
            Topology = new WindowsPortTopologyService(),
            Diagnostics = new WindowsStructuredDiagnosticsWriter(Path.Combine(root, "diagnostics.jsonl")),
            ActivityHistory = new WindowsActivityHistoryStore(Path.Combine(root, "activity-history.json")),
            UrlRisk = new WindowsUrlRiskEvaluator(),
            SecretRedactor = new WindowsSecretRedactor(),
        };
        return new WindowsCompanionCoordinator(state);
    }
}
