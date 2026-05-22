using System.Text.Json;

namespace OpenClaw.Windows;

/// <summary>
/// Structured JSONL diagnostics entry appended by the Windows companion.
/// </summary>
public sealed record WindowsDiagnosticEntry(
    DateTimeOffset CreatedAt,
    string Category,
    string Title,
    string Detail,
    string? Destination,
    string? Raw);

/// <summary>
/// Appends structured JSONL diagnostics without coupling callers to a specific storage path.
/// </summary>
public sealed class WindowsStructuredDiagnosticsWriter(string defaultPath)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string DefaultPath { get; } = defaultPath;

    public string ResolvePath(string? configuredPath)
    {
        return string.IsNullOrWhiteSpace(configuredPath)
            ? this.DefaultPath
            : configuredPath.Trim();
    }

    public async Task WriteAsync(
        string path,
        WindowsDiagnosticEntry entry,
        CancellationToken cancellationToken = default)
    {
        var directory = System.IO.Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var line = JsonSerializer.Serialize(entry, JsonOptions) + Environment.NewLine;
        await File.AppendAllTextAsync(path, line, cancellationToken);
    }
}
