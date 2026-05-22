using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using Windows.Devices.Enumeration;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Storage;

namespace OpenClaw.Windows.Native;

/// <summary>
/// Minimal media device metadata shown in the Devices page.
/// </summary>
public sealed record WindowsMediaDevice(string Id, string Name, bool IsEnabled);

/// <summary>
/// Human-readable permission/capability state for one Windows integration.
/// </summary>
public sealed record WindowsDevicePermissionStatus(string Capability, string State, string Detail);

/// <summary>
/// Result returned by native capture actions so the UI can show status and reveal files.
/// </summary>
public sealed record WindowsCaptureResult(bool Succeeded, string? Path, string Detail);

/// <summary>
/// Bounded screen recording options built on top of repeated primary-screen captures.
/// </summary>
public sealed record WindowsScreenRecordingOptions(TimeSpan Duration, int FramesPerSecond, string Prefix)
{
    public static WindowsScreenRecordingOptions Default { get; } = new(
        Duration: TimeSpan.FromSeconds(3),
        FramesPerSecond: 4,
        Prefix: "recording");
}

/// <summary>
/// Normalized recording plan after applying Windows companion limits.
/// </summary>
public sealed record WindowsScreenRecordingPlan(
    TimeSpan RequestedDuration,
    TimeSpan EffectiveDuration,
    int RequestedFramesPerSecond,
    int EffectiveFramesPerSecond,
    int FrameCount,
    int FrameDelayMs,
    string OutputDirectory,
    string Prefix)
{
    public string Summary =>
        $"Captures {this.FrameCount} frame(s) into {this.OutputDirectory} at {this.EffectiveFramesPerSecond} fps for up to {this.EffectiveDuration.TotalSeconds:0.#} second(s).";
}

/// <summary>
/// Summary of one bounded screen recording attempt.
/// </summary>
public sealed record WindowsScreenRecordingResult(
    bool Succeeded,
    string? DirectoryPath,
    string Detail,
    WindowsScreenRecordingPlan Plan,
    IReadOnlyList<WindowsCaptureResult> Frames);

/// <summary>
/// Owns Windows device probes and capture actions used by the companion Devices page.
/// </summary>
public sealed class WindowsDeviceCapabilityService
{
    public static TimeSpan MaximumScreenRecordingDuration { get; } = TimeSpan.FromSeconds(30);

    public static int MaximumScreenRecordingFramesPerSecond { get; } = 10;

    public WindowsDeviceCapabilityService(string? captureRoot = null)
    {
        this.CaptureRoot = captureRoot ?? DefaultCaptureRoot();
    }

    public string CaptureRoot { get; }

    /// <summary>
    /// Returns static and consent-gated capability descriptions without prompting the user.
    /// </summary>
    public static IReadOnlyList<WindowsDevicePermissionStatus> GetPermissionStatus()
    {
        var capabilities = WindowsHostCapabilityProbe.Current;
        return
        [
            new("Screen", capabilities.SupportsScreenCapture ? "Available" : "Unavailable", "Primary screen snapshots are captured after the user starts the action."),
            new("Screen recording", capabilities.SupportsScreenRecording ? "Available" : "Unavailable", $"Screen recording saves bounded frame sequences with limits of up to {MaximumScreenRecordingDuration.TotalSeconds:0} seconds at {MaximumScreenRecordingFramesPerSecond} fps."),
            new("Camera", capabilities.SupportsCameraCapture ? "Prompted by Windows" : "Unavailable", "Camera photo capture uses the Windows camera consent UI."),
            new("Microphone", capabilities.SupportsMicrophoneCapture ? "Prompted by Windows" : "Unavailable", "Voice controls use Windows audio devices after user consent."),
            new("Browser proxy", capabilities.SupportsBrowserProxy ? "Requires gateway" : "Unavailable", "Browser proxy routing depends on a reachable gateway/browser host and still respects URL risk policy."),
            new("System speech", capabilities.SupportsSystemTextToSpeech ? "Available" : "Unavailable", "System speech uses installed Windows voices to synthesize local reply audio."),
            new("Notifications", capabilities.SupportsToastNotifications ? "Available" : "Unavailable", "Notifications are delivered through the app tray host."),
            new("Hotkeys", capabilities.SupportsGlobalHotkeys ? "Available" : "Unavailable", "Global hotkeys are registered only while enabled in the app."),
            new("Overlays", capabilities.SupportsOverlays ? "Available" : "Unavailable", "Overlays are app-owned floating WinUI windows."),
        ];
    }

    /// <summary>
    /// Lists camera devices through WinRT device enumeration.
    /// </summary>
    public static async Task<IReadOnlyList<WindowsMediaDevice>> ListCameraDevicesAsync()
    {
        return await ListDevicesAsync(DeviceClass.VideoCapture);
    }

    /// <summary>
    /// Lists audio capture devices through WinRT device enumeration.
    /// </summary>
    public static async Task<IReadOnlyList<WindowsMediaDevice>> ListMicrophoneDevicesAsync()
    {
        return await ListDevicesAsync(DeviceClass.AudioCapture);
    }

    /// <summary>
    /// Captures a single photo using the first enabled camera, allowing Windows to request consent.
    /// </summary>
    public async Task<WindowsCaptureResult> CaptureCameraPhotoAsync()
    {
        var cameras = await ListCameraDevicesAsync();
        var camera = cameras.FirstOrDefault(device => device.IsEnabled) ?? (cameras.Count > 0 ? cameras[0] : null);
        if (camera is null)
        {
            return new WindowsCaptureResult(false, null, "No camera is available.");
        }

        Directory.CreateDirectory(this.CaptureRoot);
        var folder = await StorageFolder.GetFolderFromPathAsync(this.CaptureRoot);
        var fileName = Path.GetFileName(CreateCapturePath(this.CaptureRoot, "camera", "jpg"));
        var file = await folder.CreateFileAsync(fileName, CreationCollisionOption.GenerateUniqueName);
        var settings = new MediaCaptureInitializationSettings
        {
            VideoDeviceId = camera.Id,
            StreamingCaptureMode = StreamingCaptureMode.Video,
            PhotoCaptureSource = PhotoCaptureSource.Auto,
        };
        using var capture = new MediaCapture();
        await capture.InitializeAsync(settings);
        await capture.CapturePhotoToStorageFileAsync(ImageEncodingProperties.CreateJpeg(), file);
        return new WindowsCaptureResult(true, file.Path, $"Saved camera capture to {file.Path}");
    }

    /// <summary>
    /// Captures the primary desktop using GDI screen copy APIs.
    /// </summary>
    public WindowsCaptureResult CapturePrimaryScreen()
    {
        var path = CreateCapturePath(this.CaptureRoot, "screen", "png");
        return CapturePrimaryScreenToPath(path);
    }

    /// <summary>
    /// Captures a short sequence of primary-screen frames for quick motion/debug checks.
    /// </summary>
    public IReadOnlyList<WindowsCaptureResult> CaptureScreenFrameSequence(int frameCount = 3, int delayMs = 250)
    {
        if (frameCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frameCount), "Frame count must be greater than zero.");
        }

        var captures = new List<WindowsCaptureResult>();
        for (var index = 0; index < frameCount; index++)
        {
            captures.Add(this.CapturePrimaryScreen());
            if (index + 1 < frameCount)
            {
                Thread.Sleep(delayMs);
            }
        }

        return captures;
    }

    /// <summary>
    /// Builds a bounded recording plan so the shell can preview or persist realistic capture settings.
    /// </summary>
    public WindowsScreenRecordingPlan CreateScreenRecordingPlan(
        WindowsScreenRecordingOptions? options = null,
        DateTimeOffset? timestamp = null)
    {
        var requested = options ?? WindowsScreenRecordingOptions.Default;
        if (requested.Duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Recording duration must be greater than zero.");
        }

        if (requested.FramesPerSecond <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Recording frame rate must be greater than zero.");
        }

        var effectiveDuration = requested.Duration > MaximumScreenRecordingDuration
            ? MaximumScreenRecordingDuration
            : requested.Duration;
        var effectiveFramesPerSecond = Math.Min(requested.FramesPerSecond, MaximumScreenRecordingFramesPerSecond);
        var frameCount = Math.Max(1, (int)Math.Ceiling(effectiveDuration.TotalSeconds * effectiveFramesPerSecond));
        var frameDelayMs = Math.Max(1, (int)Math.Round(1000d / effectiveFramesPerSecond, MidpointRounding.AwayFromZero));
        var safePrefix = string.IsNullOrWhiteSpace(requested.Prefix) ? "recording" : requested.Prefix.Trim();
        var instant = timestamp ?? DateTimeOffset.Now;
        var outputDirectory = Path.Combine(this.CaptureRoot, $"{safePrefix}-{instant:yyyyMMdd-HHmmss-fff}");

        return new WindowsScreenRecordingPlan(
            RequestedDuration: requested.Duration,
            EffectiveDuration: effectiveDuration,
            RequestedFramesPerSecond: requested.FramesPerSecond,
            EffectiveFramesPerSecond: effectiveFramesPerSecond,
            FrameCount: frameCount,
            FrameDelayMs: frameDelayMs,
            OutputDirectory: outputDirectory,
            Prefix: safePrefix);
    }

    /// <summary>
    /// Captures a bounded recording as a timestamped folder of sequential PNG frames.
    /// </summary>
    public async Task<WindowsScreenRecordingResult> CaptureScreenRecordingAsync(
        WindowsScreenRecordingOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var plan = CreateScreenRecordingPlan(options);
        Directory.CreateDirectory(plan.OutputDirectory);

        var frames = new List<WindowsCaptureResult>(plan.FrameCount);
        for (var index = 0; index < plan.FrameCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var framePath = Path.Combine(plan.OutputDirectory, $"{plan.Prefix}-{index + 1:0000}.png");
            var frame = CapturePrimaryScreenToPath(framePath);
            frames.Add(frame);
            if (!frame.Succeeded)
            {
                return new WindowsScreenRecordingResult(
                    false,
                    plan.OutputDirectory,
                    $"Screen recording stopped after {frames.Count} frame(s): {frame.Detail}",
                    plan,
                    frames);
            }

            if (index + 1 < plan.FrameCount)
            {
                await Task.Delay(plan.FrameDelayMs, cancellationToken);
            }
        }

        return new WindowsScreenRecordingResult(
            true,
            plan.OutputDirectory,
            $"Captured {frames.Count} frame(s) to {plan.OutputDirectory} at {plan.EffectiveFramesPerSecond} fps for up to {plan.EffectiveDuration.TotalSeconds:0.#} second(s).",
            plan,
            frames);
    }

    /// <summary>
    /// Generates stable capture filenames that remain sortable by timestamp.
    /// </summary>
    public static string CreateCapturePath(string root, string prefix, string extension, DateTimeOffset? timestamp = null)
    {
        var safePrefix = string.IsNullOrWhiteSpace(prefix) ? "capture" : prefix.Trim();
        var safeExtension = extension.TrimStart('.');
        var instant = timestamp ?? DateTimeOffset.Now;
        return Path.Combine(root, $"{safePrefix}-{instant:yyyyMMdd-HHmmss-fff}.{safeExtension}");
    }

    private static WindowsCaptureResult CapturePrimaryScreenToPath(string path)
    {
        var screen = Screen.PrimaryScreen;
        if (screen is null)
        {
            return new WindowsCaptureResult(false, null, "No primary screen is available.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? throw new InvalidOperationException("Capture path does not include a directory."));
        using var bitmap = new Bitmap(screen.Bounds.Width, screen.Bounds.Height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(screen.Bounds.Location, Point.Empty, screen.Bounds.Size);
        bitmap.Save(path, ImageFormat.Png);
        return new WindowsCaptureResult(true, path, $"Saved primary screen snapshot to {path}");
    }

    private static async Task<IReadOnlyList<WindowsMediaDevice>> ListDevicesAsync(DeviceClass deviceClass)
    {
        var devices = await DeviceInformation.FindAllAsync(deviceClass);
        return devices
            .Select(device => new WindowsMediaDevice(device.Id, device.Name, device.IsEnabled))
            .ToArray();
    }

    private static string DefaultCaptureRoot()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(root, "OpenClaw", "WindowsCompanion", "Captures");
    }
}
