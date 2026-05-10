using OpenClaw.Windows;

namespace OpenClaw.Windows.Tests;

[TestClass]
public sealed class OnboardingCheckServiceTests
{
    [TestMethod]
    public async Task GatewayStatusCheckUsesConfiguredUrlAndGatewayToken()
    {
        var runner = new FakeGatewayCliCommandRunner(
            new GatewayCliResult(0, "OpenClaw 2026.5.8", ""),
            new GatewayCliResult(0, """{"ok":true,"service":{"installed":true,"state":"running"},"rpc":{"ok":true,"capability":"admin_capable"}}""", ""));
        var store = new AppPreferencesStore(
            Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "preferences.json"),
            new InMemoryAppCredentialStore());
        await store.SaveAsync(AppPreferences.Default with
        {
            GatewayUrl = "ws://127.0.0.1:18789",
            GatewayToken = "shared-token",
        });
        var service = new OnboardingCheckService(runner, store);

        await service.RunAsync();

        CollectionAssert.AreEqual(
            new[] { "gateway", "status", "--json", "--url", "ws://127.0.0.1:18789", "--token", "shared-token" },
            runner.Calls[1].ToArray());
    }
}
