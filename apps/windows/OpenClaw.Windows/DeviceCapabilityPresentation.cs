using OpenClaw.Windows.Native;

namespace OpenClaw.Windows;

/// <summary>
/// View-model shape for one Windows device capability card.
/// </summary>
public sealed record DeviceCapabilityPresentation(
    string Capability,
    string State,
    string Detail,
    string RepairGuidance,
    string LastAction)
{
    /// <summary>
    /// Combines the latest native probe result and last user action into display-ready text.
    /// </summary>
    public static DeviceCapabilityPresentation Create(
        string capability,
        IEnumerable<WindowsDevicePermissionStatus> statuses,
        string? lastAction = null)
    {
        var status = statuses.FirstOrDefault(
            item => string.Equals(item.Capability, capability, StringComparison.OrdinalIgnoreCase));
        var state = status?.State ?? "Not checked";
        return new DeviceCapabilityPresentation(
            capability,
            state,
            status?.Detail ?? $"Refresh devices to check {capability.ToLowerInvariant()} support.",
            RepairGuidanceFor(capability, state),
            string.IsNullOrWhiteSpace(lastAction) ? "No action run yet." : lastAction);
    }

    /// <summary>
    /// Returns operator guidance for capabilities that are missing, disabled, or waiting for consent.
    /// </summary>
    private static string RepairGuidanceFor(string capability, string state)
    {
        if (state.Equals("Available", StringComparison.OrdinalIgnoreCase))
        {
            return "No repair needed.";
        }
        if (state.Contains("Prompted", StringComparison.OrdinalIgnoreCase))
        {
            return "Windows may ask for consent when this capability is used.";
        }
        return capability switch
        {
            "Screen" => "Confirm a primary display is attached and accessible.",
            "Screen recording" => "Reduce the requested duration or frame rate and confirm the primary display remains available.",
            "Camera" => "Check Windows camera privacy settings and connect a camera.",
            "Microphone" => "Check Windows microphone privacy settings and connect an audio input.",
            "Browser proxy" => "Start the gateway, keep browser routing enabled, and leave unsafe URL blocking turned on.",
            "System speech" => "Install at least one Windows voice package or repair the Speech runtime.",
            "Notifications" => "Confirm the tray host is running and Windows notifications are enabled.",
            "Hotkeys" => "Disable conflicting shortcuts or save the hotkey toggle again.",
            "Overlays" => "Confirm desktop windowing is available for the current Windows session.",
            _ => "Refresh devices after repairing Windows permissions.",
        };
    }
}
