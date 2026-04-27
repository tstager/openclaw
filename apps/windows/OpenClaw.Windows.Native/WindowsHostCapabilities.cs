namespace OpenClaw.Windows.Native;

public sealed record WindowsHostCapabilities(
    bool SupportsTray,
    bool SupportsToastNotifications,
    bool SupportsScreenCapture,
    bool SupportsCameraCapture,
    bool SupportsGlobalHotkeys);

public static class WindowsHostCapabilityProbe
{
    public static WindowsHostCapabilities Current { get; } = new(
        SupportsTray: true,
        SupportsToastNotifications: true,
        SupportsScreenCapture: true,
        SupportsCameraCapture: true,
        SupportsGlobalHotkeys: true);
}
