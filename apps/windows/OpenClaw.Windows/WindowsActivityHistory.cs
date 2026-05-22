using System.Text.Json;

namespace OpenClaw.Windows;

/// <summary>
/// One persisted operational activity entry shown in logs/history surfaces.
/// </summary>
public sealed record WindowsActivityEntry(
    DateTimeOffset CreatedAt,
    string Category,
    string Title,
    string Detail,
    string? Destination);

/// <summary>
/// File-backed bounded activity history shared by the home, logs, and notification surfaces.
/// </summary>
public sealed class WindowsActivityHistoryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };
    private readonly object gate = new();
    private List<WindowsActivityEntry> entries;

    public WindowsActivityHistoryStore(string path)
    {
        this.Path = path;
        this.entries = LoadEntries(path);
    }

    public string Path { get; }

    public IReadOnlyList<WindowsActivityEntry> Entries
    {
        get
        {
            lock (this.gate)
            {
                return this.entries.ToArray();
            }
        }
    }

    public WindowsActivityEntry? Latest
    {
        get
        {
            lock (this.gate)
            {
                return this.entries.FirstOrDefault();
            }
        }
    }

    public async Task<WindowsActivityEntry> AddAsync(
        string category,
        string title,
        string detail,
        string? destination,
        int capacity,
        CancellationToken cancellationToken = default)
    {
        var entry = new WindowsActivityEntry(
            DateTimeOffset.Now,
            category,
            title,
            detail,
            string.IsNullOrWhiteSpace(destination) ? null : WindowsNavigationService.Normalize(destination));

        List<WindowsActivityEntry> snapshot;
        lock (this.gate)
        {
            this.entries.Insert(0, entry);
            if (this.entries.Count > capacity)
            {
                this.entries.RemoveRange(capacity, this.entries.Count - capacity);
            }

            snapshot = [.. this.entries];
        }

        await SaveEntriesAsync(this.Path, snapshot, cancellationToken);
        return entry;
    }

    private static List<WindowsActivityEntry> LoadEntries(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<WindowsActivityEntry>>(json, JsonOptions) ?? [];
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex);
            return [];
        }
    }

    private static async Task SaveEntriesAsync(
        string path,
        IReadOnlyList<WindowsActivityEntry> entries,
        CancellationToken cancellationToken)
    {
        var directory = System.IO.Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var tempPath = System.IO.Path.Combine(
            directory,
            $"{System.IO.Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                useAsync: true))
            {
                await JsonSerializer.SerializeAsync(stream, entries, JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
}
