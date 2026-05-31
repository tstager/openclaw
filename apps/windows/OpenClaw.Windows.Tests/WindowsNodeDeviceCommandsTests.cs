using System.Text.Json;
using OpenClaw.Windows;
using OpenClaw.Windows.Native;

namespace OpenClaw.Windows.Tests;

[TestClass]
public sealed class WindowsNodeDeviceCommandsTests
{
    private static JsonElement Payload(WindowsCanvasInvokeResponse response)
    {
        Assert.IsTrue(response.Ok, "Expected a success response.");
        Assert.IsNotNull(response.PayloadJson);
        return JsonDocument.Parse(response.PayloadJson).RootElement;
    }

    [TestMethod]
    public void TryParseScreenRecordingOptionsDefaultsWhenParamsMissing()
    {
        var ok = WindowsNodeDeviceCommands.TryParseScreenRecordingOptions(null, out var options, out var error);

        Assert.IsTrue(ok);
        Assert.IsNull(error);
        Assert.AreEqual(WindowsScreenRecordingOptions.Default, options);
    }

    [TestMethod]
    public void TryParseScreenRecordingOptionsReadsDurationFpsAndPrefix()
    {
        var ok = WindowsNodeDeviceCommands.TryParseScreenRecordingOptions(
            """{"durationMs":5000,"fps":6,"prefix":"clip"}""",
            out var options,
            out var error);

        Assert.IsTrue(ok);
        Assert.IsNull(error);
        Assert.AreEqual(TimeSpan.FromMilliseconds(5000), options.Duration);
        Assert.AreEqual(6, options.FramesPerSecond);
        Assert.AreEqual("clip", options.Prefix);
    }

    [TestMethod]
    public void TryParseScreenRecordingOptionsRejectsInvalidJson()
    {
        var ok = WindowsNodeDeviceCommands.TryParseScreenRecordingOptions("not json", out _, out var error);

        Assert.IsFalse(ok);
        Assert.IsNotNull(error);
        StringAssert.Contains(error, "valid JSON");
    }

    [TestMethod]
    public void TryParseScreenRecordingOptionsRejectsNonPositiveValues()
    {
        Assert.IsFalse(WindowsNodeDeviceCommands.TryParseScreenRecordingOptions(
            """{"durationMs":0}""", out _, out var durationError));
        StringAssert.Contains(durationError!, "durationMs");

        Assert.IsFalse(WindowsNodeDeviceCommands.TryParseScreenRecordingOptions(
            """{"fps":-2}""", out _, out var fpsError));
        StringAssert.Contains(fpsError!, "fps");
    }

    [TestMethod]
    public void ScreenSnapshotResponseReturnsFileMetadataOnSuccess()
    {
        var response = WindowsNodeDeviceCommands.ScreenSnapshotResponse(
            new WindowsCaptureResult(true, @"C:\captures\screen-1.png", "Saved primary screen snapshot."));

        var payload = Payload(response);
        Assert.AreEqual("png", payload.GetProperty("format").GetString());
        Assert.AreEqual(@"C:\captures\screen-1.png", payload.GetProperty("path").GetString());
    }

    [TestMethod]
    public void ScreenSnapshotResponseReturnsUnavailableWhenCaptureFails()
    {
        var response = WindowsNodeDeviceCommands.ScreenSnapshotResponse(
            new WindowsCaptureResult(false, null, "No primary screen is available."));

        Assert.IsFalse(response.Ok);
        Assert.IsNotNull(response.Error);
        Assert.AreEqual("UNAVAILABLE", response.Error.Code);
        StringAssert.Contains(response.Error.Message, "primary screen");
    }

    [TestMethod]
    public void ScreenRecordingResponseReportsFrameMetadataOnSuccess()
    {
        var plan = new WindowsScreenRecordingPlan(
            RequestedDuration: TimeSpan.FromSeconds(2),
            EffectiveDuration: TimeSpan.FromSeconds(2),
            RequestedFramesPerSecond: 4,
            EffectiveFramesPerSecond: 4,
            FrameCount: 2,
            FrameDelayMs: 250,
            OutputDirectory: @"C:\captures\recording-1",
            Prefix: "recording");
        var result = new WindowsScreenRecordingResult(
            true,
            @"C:\captures\recording-1",
            "Captured 2 frame(s).",
            plan,
            [
                new WindowsCaptureResult(true, @"C:\captures\recording-1\recording-0001.png", "ok"),
                new WindowsCaptureResult(true, @"C:\captures\recording-1\recording-0002.png", "ok"),
            ]);

        var payload = Payload(WindowsNodeDeviceCommands.ScreenRecordingResponse(result));

        Assert.AreEqual("png-sequence", payload.GetProperty("format").GetString());
        Assert.AreEqual(@"C:\captures\recording-1", payload.GetProperty("directory").GetString());
        Assert.AreEqual(2, payload.GetProperty("frameCount").GetInt32());
        Assert.AreEqual(4, payload.GetProperty("framesPerSecond").GetInt32());
        Assert.AreEqual(2, payload.GetProperty("frames").GetArrayLength());
    }

    [TestMethod]
    public void ScreenRecordingResponseReturnsUnavailableWhenRecordingFails()
    {
        var plan = new WindowsScreenRecordingPlan(
            RequestedDuration: TimeSpan.FromSeconds(2),
            EffectiveDuration: TimeSpan.FromSeconds(2),
            RequestedFramesPerSecond: 4,
            EffectiveFramesPerSecond: 4,
            FrameCount: 2,
            FrameDelayMs: 250,
            OutputDirectory: @"C:\captures\recording-1",
            Prefix: "recording");
        var result = new WindowsScreenRecordingResult(
            false,
            @"C:\captures\recording-1",
            "Screen recording stopped after 1 frame(s).",
            plan,
            [new WindowsCaptureResult(false, null, "No primary screen is available.")]);

        var response = WindowsNodeDeviceCommands.ScreenRecordingResponse(result);

        Assert.IsFalse(response.Ok);
        Assert.AreEqual("UNAVAILABLE", response.Error!.Code);
    }

    [TestMethod]
    public void CameraSnapshotResponseReturnsUnavailableWhenNoCamera()
    {
        var response = WindowsNodeDeviceCommands.CameraSnapshotResponse(
            new WindowsCaptureResult(false, null, "No camera is available."));

        Assert.IsFalse(response.Ok);
        Assert.AreEqual("UNAVAILABLE", response.Error!.Code);
        StringAssert.Contains(response.Error.Message, "camera");
    }

    [TestMethod]
    public void CameraListResponseProjectsDeviceMetadata()
    {
        var payload = Payload(WindowsNodeDeviceCommands.CameraListResponse(
            [
                new WindowsMediaDevice("cam-1", "Front Camera", true),
                new WindowsMediaDevice("cam-2", "Rear Camera", false),
            ]));

        var devices = payload.GetProperty("devices");
        Assert.AreEqual(2, devices.GetArrayLength());
        Assert.AreEqual("cam-1", devices[0].GetProperty("id").GetString());
        Assert.AreEqual("Front Camera", devices[0].GetProperty("name").GetString());
        Assert.IsTrue(devices[0].GetProperty("enabled").GetBoolean());
        Assert.IsFalse(devices[1].GetProperty("enabled").GetBoolean());
    }
}
