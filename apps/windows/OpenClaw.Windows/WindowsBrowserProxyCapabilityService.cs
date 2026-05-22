using OpenClaw.Windows.Native;

namespace OpenClaw.Windows;

/// <summary>
/// Display-ready browser proxy capability status for diagnostics and later shell wiring.
/// </summary>
public sealed record WindowsBrowserProxyStatus(
    string State,
    string Detail,
    string RepairGuidance,
    string? GatewayOrigin);

/// <summary>
/// Summarizes whether browser proxy foundations are ready without invoking UI-specific wiring.
/// </summary>
public sealed class WindowsBrowserProxyCapabilityService
{
    /// <summary>
    /// Creates a conservative readiness snapshot from saved settings and optional gateway status.
    /// </summary>
    public WindowsBrowserProxyStatus CreateStatus(
        AppPreferences preferences,
        GatewayStatusSnapshot? gatewayStatus = null)
    {
        if (!WindowsHostCapabilityProbe.Current.SupportsBrowserProxy)
        {
            return new WindowsBrowserProxyStatus(
                "Unavailable",
                "This Windows companion build does not advertise browser proxy support.",
                "Update the Windows companion build before trying browser proxy actions.",
                null);
        }

        if (!Uri.TryCreate(preferences.GatewayUrl, UriKind.Absolute, out var gatewayUri))
        {
            return new WindowsBrowserProxyStatus(
                "Misconfigured",
                "The saved gateway URL is not a valid absolute URI.",
                "Save a valid gateway URL before wiring browser proxy commands.",
                null);
        }

        var gatewayOrigin = CreateGatewayOrigin(gatewayUri);
        if (gatewayStatus is not null && !gatewayStatus.Reachable)
        {
            return new WindowsBrowserProxyStatus(
                "Gateway unavailable",
                $"Browser proxy routing is host-capable but the gateway at {gatewayOrigin} is not reachable yet.",
                "Start or repair the gateway connection before retrying browser proxy actions.",
                gatewayOrigin);
        }

        return new WindowsBrowserProxyStatus(
            "Ready for shell wiring",
            $"Browser proxy requests can be routed through the active gateway origin {gatewayOrigin} once shell commands are connected.",
            "Finish shell command wiring and keep unsafe URL blocking enabled for browser proxy usage.",
            gatewayOrigin);
    }

    private static string CreateGatewayOrigin(Uri gatewayUri)
    {
        var scheme = gatewayUri.Scheme.ToLowerInvariant() switch
        {
            "wss" => "https",
            "ws" => "http",
            _ => gatewayUri.Scheme.ToLowerInvariant(),
        };

        var builder = new UriBuilder(gatewayUri)
        {
            Scheme = scheme,
            Path = string.Empty,
            Query = string.Empty,
            Fragment = string.Empty,
        };
        return builder.Uri.GetLeftPart(UriPartial.Authority);
    }
}
