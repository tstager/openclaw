using OpenClaw.Windows;
using OpenClaw.Windows.Native;

namespace OpenClaw.Windows.Tests;

[TestClass]
public sealed class WindowsNodeSurfaceTests
{
    private static WindowsHostCapabilities FullHost(
        bool screenCapture = true,
        bool screenRecording = true,
        bool camera = true,
        bool microphone = true,
        bool notifications = true)
    {
        return new WindowsHostCapabilities(
            SupportsTray: true,
            SupportsToastNotifications: notifications,
            SupportsScreenCapture: screenCapture,
            SupportsScreenRecording: screenRecording,
            SupportsCameraCapture: camera,
            SupportsMicrophoneCapture: microphone,
            SupportsBrowserProxy: true,
            SupportsSystemTextToSpeech: true,
            SupportsGlobalHotkeys: true,
            SupportsOverlays: true);
    }

    [TestMethod]
    public void DefaultPreferencesAdvertiseCanvasScreenAndCameraSurface()
    {
        var surface = WindowsNodeSurface.Build(WindowsHostCapabilityProbe.Current, AppPreferences.Default);

        CollectionAssert.AreEqual(new[] { "canvas", "screen", "camera" }, surface.Capabilities.ToArray());
        CollectionAssert.AreEqual(
            new[]
            {
                "canvas.present",
                "canvas.hide",
                "canvas.navigate",
                "canvas.eval",
                "canvas.snapshot",
                "canvas.a2ui.push",
                "canvas.a2ui.pushJSONL",
                "canvas.a2ui.reset",
                "screen.snapshot",
                "screen.record",
                "camera.list",
                "camera.snap",
            },
            surface.Commands.ToArray());
        Assert.IsTrue(surface.Permissions["canvas.a2ui"]);
        Assert.IsTrue(surface.Permissions["screen.record"]);
        Assert.IsTrue(surface.Permissions["camera.capture"]);
        Assert.IsFalse(surface.Permissions["microphone"]);
        Assert.IsTrue(surface.Permissions["notifications"]);
    }

    [TestMethod]
    public void DisablingCanvasNodeRemovesCanvasCommandsAndCapability()
    {
        var preferences = AppPreferences.Default with { CanvasNodeEnabled = false };

        var surface = WindowsNodeSurface.Build(FullHost(), preferences);

        CollectionAssert.DoesNotContain(surface.Capabilities.ToArray(), "canvas");
        Assert.IsFalse(surface.Commands.Any(command => command.StartsWith("canvas.", StringComparison.Ordinal)));
        Assert.IsFalse(surface.Permissions.ContainsKey("canvas.a2ui"));
        CollectionAssert.AreEqual(new[] { "screen", "camera" }, surface.Capabilities.ToArray());
    }

    [TestMethod]
    public void VoiceControlsPreferenceDrivesMicrophonePermission()
    {
        var enabled = WindowsNodeSurface.Build(
            FullHost(),
            AppPreferences.Default with { VoiceControlsEnabled = true });

        Assert.IsTrue(enabled.Permissions["microphone"]);
    }

    [TestMethod]
    public void HostWithoutCameraDropsCameraSurface()
    {
        var surface = WindowsNodeSurface.Build(FullHost(camera: false), AppPreferences.Default);

        CollectionAssert.DoesNotContain(surface.Capabilities.ToArray(), "camera");
        Assert.IsFalse(surface.Commands.Any(command => command.StartsWith("camera.", StringComparison.Ordinal)));
        Assert.IsFalse(surface.Permissions.ContainsKey("camera.capture"));
    }

    [TestMethod]
    public void HostWithoutScreenRecordingStillAdvertisesScreenSnapshot()
    {
        var surface = WindowsNodeSurface.Build(FullHost(screenRecording: false), AppPreferences.Default);

        CollectionAssert.Contains(surface.Commands.ToArray(), "screen.snapshot");
        CollectionAssert.DoesNotContain(surface.Commands.ToArray(), "screen.record");
        Assert.IsFalse(surface.Permissions["screen.record"]);
    }
}
