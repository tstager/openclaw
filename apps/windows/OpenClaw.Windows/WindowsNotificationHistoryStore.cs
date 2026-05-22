using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenClaw.Windows;

/// <summary>
/// File-backed bounded notification history used by future activity and notifications surfaces.
/// </summary>
public sealed class WindowsNotificationHistoryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter(),
        },
    };

    private readonly object gate = new();
    private List<WindowsNotificationActivity> entries;

    public WindowsNotificationHistoryStore(string path)
    {
        this.Path = path;
        this.entries = LoadEntries(path);
    }

    public string Path { get; }

    public IReadOnlyList<WindowsNotificationActivity> Entries
    {
        get
        {
            lock (this.gate)
            {
                return this.entries.ToArray();
            }
        }
    }

    public WindowsNotificationActivity? Latest
    {
        get
        {
            lock (this.gate)
            {
                return this.entries.FirstOrDefault();
            }
        }
    }

    public async Task<WindowsNotificationActivity> AddAsync(
        string destination,
        string title,
        string message,
        int capacity,
        string category = WindowsNotificationCategories.General,
        WindowsNotificationKind kind = WindowsNotificationKind.Unknown,
        CancellationToken cancellationToken = default)
    {
        var entry = new WindowsNotificationActivity(
            DateTimeOffset.Now,
            NormalizeDestination(destination),
            title,
            message,
            WindowsNotificationCategories.Normalize(category),
            kind);

        List<WindowsNotificationActivity> snapshot;
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

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        lock (this.gate)
        {
            this.entries.Clear();
        }

        await SaveEntriesAsync(this.Path, [], cancellationToken);
    }

    private static List<WindowsNotificationActivity> LoadEntries(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<WindowsNotificationActivity>>(json, JsonOptions) ?? [];
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex);
            return [];
        }
    }

    private static string NormalizeDestination(string destination)
    {
        return string.IsNullOrWhiteSpace(destination)
            ? WindowsNavigationDestination.Home
            : WindowsNavigationService.Normalize(destination.Trim());
    }

    private static async Task SaveEntriesAsync(
        string path,
        IReadOnlyList<WindowsNotificationActivity> entries,
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
