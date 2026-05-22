using OpenClaw.Windows;

namespace OpenClaw.Windows.Tests;

[TestClass]
public sealed class WindowsBrowserProxyCapabilityServiceTests
{
    [TestMethod]
    public void InvalidGatewayUrlShowsMisconfiguredStatus()
    {
        var service = new WindowsBrowserProxyCapabilityService();
        var status = service.CreateStatus(AppPreferences.Default with { GatewayUrl = "not-a-uri" });

        Assert.AreEqual("Misconfigured", status.State);
        StringAssert.Contains(status.RepairGuidance, "Save a valid gateway URL");
        Assert.IsNull(status.GatewayOrigin);
    }

    [TestMethod]
    public void ReachableGatewayShowsReadyStatus()
    {
        var service = new WindowsBrowserProxyCapabilityService();
        var gatewayStatus = GatewayStatusSnapshot.FromJson(
            """{"ok":true,"service":{"installed":true,"state":"running"},"rpc":{"ok":true,"capability":"browser_proxy"},"dashboard":{"url":"http://127.0.0.1:18789"}}""");

        var status = service.CreateStatus(AppPreferences.Default with { GatewayUrl = "ws://127.0.0.1:18789" }, gatewayStatus);

        Assert.AreEqual("Ready for shell wiring", status.State);
        Assert.AreEqual("http://127.0.0.1:18789", status.GatewayOrigin);
    }
}
