using System.Globalization;
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
            DateTimeOffset.Parse("2026-04-27T15:16:17.123Z", CultureInfo.InvariantCulture));

        Assert.AreEqual(Path.Combine("captures", "camera-20260427-151617-123.jpg"), path);
    }

    [TestMethod]
    public void PermissionStatusIncludesStepFourCapabilities()
    {
        var statuses = WindowsDeviceCapabilityService.GetPermissionStatus();

        CollectionAssert.IsSubsetOf(
            new[] { "Screen", "Screen recording", "Camera", "Microphone", "Browser proxy", "System speech", "Notifications", "Hotkeys", "Overlays" },
            statuses.Select(status => status.Capability).ToArray());
    }

    [TestMethod]
    public void ScreenRecordingPlanClampsDurationAndFrameRate()
    {
        var service = new WindowsDeviceCapabilityService("captures");

        var plan = service.CreateScreenRecordingPlan(
            new WindowsScreenRecordingOptions(TimeSpan.FromMinutes(2), 24, "recording"),
            DateTimeOffset.Parse("2026-04-27T15:16:17.123Z", CultureInfo.InvariantCulture));

        Assert.AreEqual(WindowsDeviceCapabilityService.MaximumScreenRecordingDuration, plan.EffectiveDuration);
        Assert.AreEqual(WindowsDeviceCapabilityService.MaximumScreenRecordingFramesPerSecond, plan.EffectiveFramesPerSecond);
        Assert.AreEqual(Path.Combine("captures", "recording-20260427-151617-123"), plan.OutputDirectory);
        Assert.AreEqual(300, plan.FrameCount);
    }
}
