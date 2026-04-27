using System.Text.Json;

namespace OpenClaw.Windows;

public sealed record AppPreferences(
    bool OpenMainWindowOnLaunch,
    string GatewayUrl,
    string? GatewayToken,
    string? DeviceToken,
    string ChatSessionKey,
    string? LastStatus,
    DateTimeOffset? LastStatusCheckedAt)
{
    public static AppPreferences Default { get; } = new(
        OpenMainWindowOnLaunch: true,
        GatewayUrl: "ws://127.0.0.1:18789",
        GatewayToken: null,
        DeviceToken: null,
        ChatSessionKey: "main",
        LastStatus: null,
        LastStatusCheckedAt: null);
}

public sealed class AppPreferencesStore(string path)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public string Path { get; } = path;

    public static AppPreferencesStore CreateDefault()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return new AppPreferencesStore(System.IO.Path.Combine(root, "OpenClaw", "WindowsCompanion", "preferences.json"));
    }

    public async Task<AppPreferences> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(this.Path))
        {
            return AppPreferences.Default;
        }

        await using var stream = File.OpenRead(this.Path);
        return await JsonSerializer.DeserializeAsync<AppPreferences>(stream, JsonOptions, cancellationToken) ??
            AppPreferences.Default;
    }

    public async Task SaveAsync(AppPreferences preferences, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(this.Path)!);
        await using var stream = File.Create(this.Path);
        await JsonSerializer.SerializeAsync(stream, preferences, JsonOptions, cancellationToken);
    }

    public async Task<AppPreferences> UpdateAsync(
        Func<AppPreferences, AppPreferences> update,
        CancellationToken cancellationToken = default)
    {
        var next = update(await this.LoadAsync(cancellationToken));
        await this.SaveAsync(next, cancellationToken);
        return next;
    }
}
