using System.Text.Json;
using OpenClaw.Windows.Native;

namespace OpenClaw.Windows;

/// <summary>
/// Pure parameter parsing and result shaping for the native Windows node device commands.
/// </summary>
/// <remarks>
/// These helpers turn the shared <see cref="WindowsDeviceCapabilityService"/> results — the same service the
/// Devices page drives — into structured <c>node.invoke</c> responses. Keeping parsing and payload shaping here
/// (off the UI thread, free of capture side effects) makes the command surface unit-testable.
/// </remarks>
public static class WindowsNodeDeviceCommands
{
    public const string ScreenSnapshot = "screen.snapshot";
    public const string ScreenRecord = "screen.record";
    public const string CameraList = "camera.list";
    public const string CameraSnap = "camera.snap";

    /// <summary>
    /// Parses bounded screen-recording options from invoke params. Missing fields fall back to defaults; the
    /// service applies the hard duration/fps limits.
    /// </summary>
    public static bool TryParseScreenRecordingOptions(
        string? paramsJson,
        out WindowsScreenRecordingOptions options,
        out string? error)
    {
        options = WindowsScreenRecordingOptions.Default;
        error = null;
        if (string.IsNullOrWhiteSpace(paramsJson))
        {
            return true;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(paramsJson);
        }
        catch (JsonException)
        {
            error = "screen.record params must be valid JSON.";
            return false;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                error = "screen.record params must be a JSON object.";
                return false;
            }

            var duration = WindowsScreenRecordingOptions.Default.Duration;
            var framesPerSecond = WindowsScreenRecordingOptions.Default.FramesPerSecond;
            var prefix = WindowsScreenRecordingOptions.Default.Prefix;

            if (root.TryGetProperty("durationMs", out var durationValue))
            {
                if (durationValue.ValueKind != JsonValueKind.Number ||
                    !durationValue.TryGetInt32(out var durationMs) ||
                    durationMs <= 0)
                {
                    error = "durationMs must be a positive integer.";
                    return false;
                }
                duration = TimeSpan.FromMilliseconds(durationMs);
            }

            if (root.TryGetProperty("fps", out var fpsValue))
            {
                if (fpsValue.ValueKind != JsonValueKind.Number ||
                    !fpsValue.TryGetInt32(out var fps) ||
                    fps <= 0)
                {
                    error = "fps must be a positive integer.";
                    return false;
                }
                framesPerSecond = fps;
            }

            if (root.TryGetProperty("prefix", out var prefixValue) &&
                prefixValue.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(prefixValue.GetString()))
            {
                prefix = prefixValue.GetString()!;
            }

            options = new WindowsScreenRecordingOptions(duration, framesPerSecond, prefix);
            return true;
        }
    }

    public static WindowsCanvasInvokeResponse ScreenSnapshotResponse(WindowsCaptureResult result)
    {
        if (!result.Succeeded || string.IsNullOrWhiteSpace(result.Path))
        {
            return WindowsCanvasInvokeResponse.Failure("UNAVAILABLE", result.Detail);
        }

        return WindowsCanvasInvokeResponse.Success(JsonSerializer.Serialize(new
        {
            format = "png",
            path = result.Path,
            detail = result.Detail,
        }));
    }

    public static WindowsCanvasInvokeResponse ScreenRecordingResponse(WindowsScreenRecordingResult result)
    {
        var frames = result.Frames
            .Where(frame => frame.Succeeded && !string.IsNullOrWhiteSpace(frame.Path))
            .Select(frame => frame.Path)
            .ToArray();

        if (!result.Succeeded || string.IsNullOrWhiteSpace(result.DirectoryPath))
        {
            return WindowsCanvasInvokeResponse.Failure("UNAVAILABLE", result.Detail);
        }

        return WindowsCanvasInvokeResponse.Success(JsonSerializer.Serialize(new
        {
            format = "png-sequence",
            directory = result.DirectoryPath,
            frameCount = frames.Length,
            framesPerSecond = result.Plan.EffectiveFramesPerSecond,
            durationSeconds = result.Plan.EffectiveDuration.TotalSeconds,
            frames,
            detail = result.Detail,
        }));
    }

    public static WindowsCanvasInvokeResponse CameraSnapshotResponse(WindowsCaptureResult result)
    {
        if (!result.Succeeded || string.IsNullOrWhiteSpace(result.Path))
        {
            return WindowsCanvasInvokeResponse.Failure("UNAVAILABLE", result.Detail);
        }

        return WindowsCanvasInvokeResponse.Success(JsonSerializer.Serialize(new
        {
            format = "jpg",
            path = result.Path,
            detail = result.Detail,
        }));
    }

    public static WindowsCanvasInvokeResponse CameraListResponse(IReadOnlyList<WindowsMediaDevice> devices)
    {
        return WindowsCanvasInvokeResponse.Success(JsonSerializer.Serialize(new
        {
            devices = devices.Select(device => new
            {
                id = device.Id,
                name = device.Name,
                enabled = device.IsEnabled,
            }),
        }));
    }
}
