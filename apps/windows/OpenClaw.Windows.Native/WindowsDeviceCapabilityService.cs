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
/// Owns Windows device probes and capture actions used by the companion Devices page.
/// </summary>
public sealed class WindowsDeviceCapabilityService
{
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
            new("Camera", capabilities.SupportsCameraCapture ? "Prompted by Windows" : "Unavailable", "Camera photo capture uses the Windows camera consent UI."),
            new("Microphone", capabilities.SupportsMicrophoneCapture ? "Prompted by Windows" : "Unavailable", "Voice controls use Windows audio devices after user consent."),
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
        var screen = Screen.PrimaryScreen;
        if (screen is null)
        {
            return new WindowsCaptureResult(false, null, "No primary screen is available.");
        }

        Directory.CreateDirectory(this.CaptureRoot);
        var path = CreateCapturePath(this.CaptureRoot, "screen", "png");
        using var bitmap = new Bitmap(screen.Bounds.Width, screen.Bounds.Height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(screen.Bounds.Location, Point.Empty, screen.Bounds.Size);
        bitmap.Save(path, ImageFormat.Png);
        return new WindowsCaptureResult(true, path, $"Saved primary screen snapshot to {path}");
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
    /// Generates stable capture filenames that remain sortable by timestamp.
    /// </summary>
    public static string CreateCapturePath(string root, string prefix, string extension, DateTimeOffset? timestamp = null)
    {
        var safePrefix = string.IsNullOrWhiteSpace(prefix) ? "capture" : prefix.Trim();
        var safeExtension = extension.TrimStart('.');
        var instant = timestamp ?? DateTimeOffset.Now;
        return Path.Combine(root, $"{safePrefix}-{instant:yyyyMMdd-HHmmss-fff}.{safeExtension}");
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
