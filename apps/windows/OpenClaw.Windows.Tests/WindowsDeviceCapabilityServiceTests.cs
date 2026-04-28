using OpenClaw.Windows.Native;

namespace OpenClaw.Windows.Tests;

[TestClass]
public sealed class WindowsDeviceCapabilityServiceTests
{
    [TestMethod]
    public void CapturePathUsesStableTimestampFormat()
    {
        var path = WindowsDeviceCapabilityService.CreateCapturePath(
            "captures",
            "camera",
            "jpg",
            DateTimeOffset.Parse("2026-04-27T15:16:17.123Z"));

        Assert.AreEqual(Path.Combine("captures", "camera-20260427-151617-123.jpg"), path);
    }

    [TestMethod]
    public void PermissionStatusIncludesStepFourCapabilities()
    {
        var service = new WindowsDeviceCapabilityService("captures");
        var statuses = service.GetPermissionStatus();

        CollectionAssert.IsSubsetOf(
            new[] { "Screen", "Camera", "Microphone", "Notifications", "Hotkeys", "Overlays" },
            statuses.Select(status => status.Capability).ToArray());
    }
}
