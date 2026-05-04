using OpenClaw.Windows;

namespace OpenClaw.Windows.Tests;

[TestClass]
public sealed class GatewayCompanionControllerTests
{
    [TestMethod]
    public async Task GatewayActionsUseCliLifecycleContracts()
    {
        var runner = new FakeGatewayCliCommandRunner(
            new GatewayCliResult(0, """{"ok":true,"service":{"installed":true,"state":"running"},"rpc":{"ok":true,"capability":"admin_capable"}}""", ""));
        var store = new AppPreferencesStore(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "preferences.json"));
        var controller = new GatewayCompanionController(runner, store);

        await controller.RunActionAsync(GatewayCliAction.Restart);

        CollectionAssert.AreEqual(new[] { "gateway", "restart", "--json" }, runner.Calls[0].ToArray());
        CollectionAssert.AreEqual(new[] { "gateway", "status", "--json" }, runner.Calls[1].ToArray());
    }

    [TestMethod]
    public async Task GatewayActionStopsAfterMissingCliFailure()
    {
        var runner = new FakeGatewayCliCommandRunner(
            new GatewayCliResult(1, "", "OpenClaw CLI was not found."));
        var store = new AppPreferencesStore(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "preferences.json"));
        var controller = new GatewayCompanionController(runner, store);

        var result = await controller.RunActionAsync(GatewayCliAction.Start);

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual("unavailable", result.Status.State);
        StringAssert.Contains(result.Output, "OpenClaw CLI was not found");
        Assert.HasCount(1, runner.Calls);
        CollectionAssert.AreEqual(new[] { "gateway", "start", "--json" }, runner.Calls[0].ToArray());
    }

    [TestMethod]
    public void ParsesGatewayStatusJson()
    {
        var snapshot = GatewayStatusSnapshot.FromJson(
            """{"ok":true,"service":{"installed":true,"state":"running"},"rpc":{"ok":true,"capability":"admin_capable"},"dashboard":{"url":"http://127.0.0.1:18080"},"logs":{"file":"C:\\openclaw.log"}}""");

        Assert.AreEqual("running", snapshot.State);
        Assert.IsTrue(snapshot.ServiceInstalled);
        Assert.IsTrue(snapshot.Reachable);
        Assert.AreEqual("admin_capable", snapshot.Capability);
        Assert.AreEqual("http://127.0.0.1:18080", snapshot.DashboardUrl);
        Assert.AreEqual(@"C:\openclaw.log", snapshot.LogPath);
    }
}
