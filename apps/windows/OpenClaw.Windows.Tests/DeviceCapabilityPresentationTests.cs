using OpenClaw.Windows;
using OpenClaw.Windows.Native;

namespace OpenClaw.Windows.Tests;

[TestClass]
public sealed class DeviceCapabilityPresentationTests
{
    [TestMethod]
    public void AvailableCapabilityShowsNoRepairNeeded()
    {
        var presentation = DeviceCapabilityPresentation.Create(
            "Screen",
            [new WindowsDevicePermissionStatus("Screen", "Available", "Primary screen snapshots are available.")],
            "Saved screen capture.");

        Assert.AreEqual("Screen", presentation.Capability);
        Assert.AreEqual("Available", presentation.State);
        Assert.AreEqual("Primary screen snapshots are available.", presentation.Detail);
        Assert.AreEqual("No repair needed.", presentation.RepairGuidance);
        Assert.AreEqual("Saved screen capture.", presentation.LastAction);
    }

    [TestMethod]
    public void PromptedCapabilityExplainsWindowsConsent()
    {
        var presentation = DeviceCapabilityPresentation.Create(
            "Camera",
            [new WindowsDevicePermissionStatus("Camera", "Prompted by Windows", "Camera photo capture uses consent UI.")]);

        Assert.AreEqual("Prompted by Windows", presentation.State);
        Assert.AreEqual("Windows may ask for consent when this capability is used.", presentation.RepairGuidance);
        Assert.AreEqual("No action run yet.", presentation.LastAction);
    }

    [TestMethod]
    public void MissingCapabilityShowsRefreshGuidance()
    {
        var presentation = DeviceCapabilityPresentation.Create("Notifications", []);

        Assert.AreEqual("Not checked", presentation.State);
        Assert.AreEqual("Refresh devices to check notifications support.", presentation.Detail);
        Assert.AreEqual("Confirm the tray host is running and Windows notifications are enabled.", presentation.RepairGuidance);
    }

    [TestMethod]
    public void BrowserProxyCapabilityShowsGatewayRepairGuidance()
    {
        var presentation = DeviceCapabilityPresentation.Create(
            "Browser proxy",
            [new WindowsDevicePermissionStatus("Browser proxy", "Requires gateway", "Browser proxy routing depends on a reachable gateway/browser host.")]);

        Assert.AreEqual("Requires gateway", presentation.State);
        Assert.AreEqual("Start the gateway, keep browser routing enabled, and leave unsafe URL blocking turned on.", presentation.RepairGuidance);
    }
}
