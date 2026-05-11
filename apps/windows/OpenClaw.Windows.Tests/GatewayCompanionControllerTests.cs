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
    public async Task GatewayStatusUsesConfiguredUrlAndGatewayToken()
    {
        var runner = new FakeGatewayCliCommandRunner(
            new GatewayCliResult(0, """{"ok":true,"service":{"installed":true,"state":"running"},"rpc":{"ok":true,"capability":"admin_capable"}}""", ""));
        var store = new AppPreferencesStore(
            Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "preferences.json"),
            new InMemoryAppCredentialStore());
        await store.SaveAsync(AppPreferences.Default with
        {
            GatewayUrl = "ws://127.0.0.1:18789",
            GatewayToken = "shared-token",
        });
        var controller = new GatewayCompanionController(runner, store);

        await controller.RefreshStatusAsync();

        CollectionAssert.AreEqual(
            new[] { "gateway", "status", "--json", "--url", "ws://127.0.0.1:18789", "--token", "shared-token" },
            runner.Calls[0].ToArray());
    }

    [TestMethod]
    public async Task GatewayStatusDerivesDashboardUrlFromConfiguredGatewayUrl()
    {
        var runner = new FakeGatewayCliCommandRunner(
            new GatewayCliResult(0, """{"ok":true,"service":{"installed":true,"state":"running"},"rpc":{"ok":true,"capability":"admin_capable"}}""", ""));
        var store = new AppPreferencesStore(
            Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "preferences.json"),
            new InMemoryAppCredentialStore());
        await store.SaveAsync(AppPreferences.Default with
        {
            GatewayUrl = "ws://127.0.0.1:18789",
            GatewayToken = "shared-token",
        });
        var controller = new GatewayCompanionController(runner, store);

        var status = await controller.RefreshStatusAsync();

        Assert.AreEqual("http://127.0.0.1:18789/", status.DashboardUrl);
    }

    [TestMethod]
    public async Task GatewayInstallUsesConfiguredGatewayToken()
    {
        var runner = new FakeGatewayCliCommandRunner(
            new GatewayCliResult(0, """{"ok":true,"service":{"installed":true,"state":"running"},"rpc":{"ok":true,"capability":"admin_capable"}}""", ""),
            new GatewayCliResult(0, """{"ok":true,"service":{"installed":true,"state":"running"},"rpc":{"ok":true,"capability":"admin_capable"}}""", ""));
        var store = new AppPreferencesStore(
            Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "preferences.json"),
            new InMemoryAppCredentialStore());
        await store.SaveAsync(AppPreferences.Default with
        {
            GatewayToken = "shared-token",
        });
        var controller = new GatewayCompanionController(runner, store);

        await controller.RunActionAsync(GatewayCliAction.Install);

        CollectionAssert.AreEqual(
            new[] { "gateway", "install", "--json", "--token", "shared-token" },
            runner.Calls[0].ToArray());
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

    [TestMethod]
    public void ParsesCurrentGatewayStatusJsonTargetShape()
    {
        var snapshot = GatewayStatusSnapshot.FromJson(
            """
            {
              "ok": true,
              "capability": "write_capable",
              "primaryTargetId": "localLoopback",
              "network": {
                "localLoopbackUrl": "ws://127.0.0.1:18789"
              },
              "targets": [
                {
                  "id": "tailnet",
                  "connect": {
                    "ok": false,
                    "rpcOk": false
                  },
                  "auth": {
                    "capability": "read_only"
                  }
                },
                {
                  "id": "localLoopback",
                  "connect": {
                    "ok": true,
                    "rpcOk": true
                  },
                  "auth": {
                    "capability": "write_capable"
                  },
                  "config": {
                    "gateway": {
                      "controlUiBasePath": "/control"
                    }
                  }
                }
              ]
            }
            """);

        Assert.AreEqual("running", snapshot.State);
        Assert.IsTrue(snapshot.Reachable);
        Assert.AreEqual("write_capable", snapshot.Capability);
        Assert.AreEqual("http://127.0.0.1:18789/control/", snapshot.DashboardUrl);
    }

    [TestMethod]
    public void UsesPrimaryTargetCapabilityWhenSummaryCapabilityIsMissing()
    {
        var snapshot = GatewayStatusSnapshot.FromJson(
            """
            {
              "primaryTargetId": "localLoopback",
              "targets": [
                {
                  "id": "localLoopback",
                  "connect": {
                    "ok": true,
                    "rpcOk": true
                  },
                  "auth": {
                    "capability": "write_capable"
                  }
                }
              ]
            }
            """);

        Assert.AreEqual("running", snapshot.State);
        Assert.IsTrue(snapshot.Reachable);
        Assert.AreEqual("write_capable", snapshot.Capability);
    }

    [TestMethod]
    public void DerivesDashboardUrlFromPrimaryTargetWhenNetworkHintIsMissing()
    {
        var snapshot = GatewayStatusSnapshot.FromJson(
            """
            {
              "primaryTargetId": "localLoopback",
              "targets": [
                {
                  "id": "localLoopback",
                  "url": "wss://127.0.0.1:18789",
                  "connect": {
                    "ok": true,
                    "rpcOk": true
                  },
                  "config": {
                    "gateway": {
                      "controlUiBasePath": ""
                    }
                  }
                }
              ]
            }
            """);

        Assert.AreEqual("https://127.0.0.1:18789/", snapshot.DashboardUrl);
    }
}
