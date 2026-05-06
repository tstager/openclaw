using System.Globalization;

namespace OpenClaw.Windows;

public sealed record LogsDiagnosticsSummary(
    string AppLogPath,
    string GatewayLogPath,
    string AppLogFolderPath,
    string GatewayLogFolderPath,
    bool CanUseAppLogActions,
    bool CanUseGatewayLogActions,
    string GatewayStatus,
    string LastError,
    string LastRefresh)
{
    public static LogsDiagnosticsSummary Create(
        string appLogPath,
        GatewayStatusSnapshot? gatewayStatus,
        string? lastError,
        DateTimeOffset? lastRefresh)
    {
        return new LogsDiagnosticsSummary(
            AppLogPath: string.IsNullOrWhiteSpace(appLogPath) ? "unknown" : appLogPath,
            GatewayLogPath: string.IsNullOrWhiteSpace(gatewayStatus?.LogPath) ? "unknown" : gatewayStatus.LogPath!,
            AppLogFolderPath: FolderPath(appLogPath),
            GatewayLogFolderPath: FolderPath(gatewayStatus?.LogPath),
            CanUseAppLogActions: HasKnownPath(appLogPath),
            CanUseGatewayLogActions: HasKnownPath(gatewayStatus?.LogPath),
            GatewayStatus: gatewayStatus?.State ?? "unknown",
            LastError: string.IsNullOrWhiteSpace(lastError) ? "none" : lastError,
            LastRefresh: lastRefresh?.ToLocalTime().ToString("g", CultureInfo.CurrentCulture) ?? "never");
    }

    private static bool HasKnownPath(string? path)
    {
        return !string.IsNullOrWhiteSpace(path) &&
            !string.Equals(path, "unknown", StringComparison.OrdinalIgnoreCase);
    }

    private static string FolderPath(string? path)
    {
        if (!HasKnownPath(path))
        {
            return "unknown";
        }

        return Path.GetDirectoryName(path) ?? path!;
    }
}
