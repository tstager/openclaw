namespace OpenClaw.Windows.Native;

/// <summary>
/// Describes which native Windows companion surfaces are expected to be available on this host.
/// </summary>
public sealed record WindowsHostCapabilities(
    bool SupportsTray,
    bool SupportsToastNotifications,
    bool SupportsScreenCapture,
    bool SupportsCameraCapture,
    bool SupportsMicrophoneCapture,
    bool SupportsGlobalHotkeys,
    bool SupportsOverlays);

/// <summary>
/// Reports static capability availability for the current Windows app session.
/// </summary>
public static class WindowsHostCapabilityProbe
{
    public static WindowsHostCapabilities Current { get; } = new(
        SupportsTray: true,
        SupportsToastNotifications: true,
        SupportsScreenCapture: true,
        SupportsCameraCapture: true,
        SupportsMicrophoneCapture: true,
        SupportsGlobalHotkeys: true,
        SupportsOverlays: true);
}
