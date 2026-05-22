using System.Globalization;
using System.Net.NetworkInformation;

namespace OpenClaw.Windows;

/// <summary>
/// One detected endpoint or port-health row shown in topology surfaces.
/// </summary>
public sealed record WindowsPortDiagnostic(
    string Label,
    string Endpoint,
    string State,
    string Detail);

/// <summary>
/// Display-ready topology summary for the gateway, dashboard, canvas, and optional SSH tunnel.
/// </summary>
public sealed record WindowsTopologySnapshot(
    string TunnelSummary,
    IReadOnlyList<WindowsPortDiagnostic> Diagnostics);

/// <summary>
/// Builds Windows-friendly topology diagnostics from saved settings and the current runtime state.
/// </summary>
public sealed class WindowsPortTopologyService
{
    public WindowsTopologySnapshot CreateSnapshot(
        AppPreferences preferences,
        GatewayStatusSnapshot? gatewayStatus,
        string? canvasA2uiUrl,
        WindowsSshTunnelStatus tunnelStatus)
    {
        var diagnostics = new List<WindowsPortDiagnostic>();
        AddEndpointDiagnostic(diagnostics, "Gateway", preferences.GatewayUrl);
        AddEndpointDiagnostic(diagnostics, "Dashboard", gatewayStatus?.DashboardUrl);
        AddEndpointDiagnostic(diagnostics, "Canvas A2UI", canvasA2uiUrl);
        if (preferences.Topology.LocalPort > 0)
        {
            diagnostics.Add(BuildLocalPortDiagnostic(
                "SSH tunnel",
                preferences.Topology.LocalPort,
                tunnelStatus.Running,
                tunnelStatus.LastError));
        }

        return new WindowsTopologySnapshot(
            tunnelStatus.Summary,
            diagnostics);
    }

    private static void AddEndpointDiagnostic(List<WindowsPortDiagnostic> diagnostics, string label, string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint) || !Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            diagnostics.Add(new WindowsPortDiagnostic(label, "unknown", "unavailable", "No endpoint is available."));
            return;
        }

        diagnostics.Add(IsLoopbackHost(uri)
            ? BuildLocalPortDiagnostic(label, uri.Port, IsPortListening(uri.Port), null, endpoint)
            : new WindowsPortDiagnostic(label, endpoint, "remote", $"{uri.Host}:{uri.Port.ToString(CultureInfo.InvariantCulture)}"));
    }

    private static WindowsPortDiagnostic BuildLocalPortDiagnostic(
        string label,
        int port,
        bool isAvailable,
        string? detail,
        string? endpoint = null)
    {
        return new WindowsPortDiagnostic(
            label,
            endpoint ?? $"127.0.0.1:{port.ToString(CultureInfo.InvariantCulture)}",
            isAvailable ? "listening" : "not listening",
            string.IsNullOrWhiteSpace(detail)
                ? (isAvailable ? "A local listener was detected." : "No local listener was detected.")
                : detail);
    }

    private static bool IsLoopbackHost(Uri uri)
    {
        return uri.IsLoopback ||
            string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPortListening(int port)
    {
        return IPGlobalProperties.GetIPGlobalProperties()
            .GetActiveTcpListeners()
            .Any(endpoint => endpoint.Port == port);
    }
}
