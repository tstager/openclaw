using System.Text.Json;

namespace OpenClaw.Windows;

public sealed record AppPreferences(
    bool OpenMainWindowOnLaunch,
    string GatewayUrl,
    string? GatewayToken,
    string? DeviceToken,
    string ChatSessionKey,
    bool VoiceControlsEnabled,
    bool GlobalHotkeyEnabled,
    string? LastStatus,
    DateTimeOffset? LastStatusCheckedAt,
    WindowsNotificationPreferences NotificationPreferences)
{
    public static AppPreferences Default { get; } = new(
        OpenMainWindowOnLaunch: true,
        GatewayUrl: "ws://127.0.0.1:18789",
        GatewayToken: null,
        DeviceToken: null,
        ChatSessionKey: "main",
        VoiceControlsEnabled: false,
        GlobalHotkeyEnabled: false,
        LastStatus: null,
        LastStatusCheckedAt: null,
        NotificationPreferences: WindowsNotificationPreferences.Default);
}

public sealed class AppPreferencesStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };
    private readonly IAppCredentialStore? credentials;
    private readonly SemaphoreSlim gate = new(1, 1);

    public AppPreferencesStore(string path, IAppCredentialStore? credentials = null)
    {
        this.Path = path;
        this.credentials = credentials;
    }

    public string Path { get; }

    public static AppPreferencesStore CreateDefault(IAppCredentialStore credentials)
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return new AppPreferencesStore(
            System.IO.Path.Combine(root, "OpenClaw", "WindowsCompanion", "preferences.json"),
            credentials);
    }

    public async Task<AppPreferences> LoadAsync(CancellationToken cancellationToken = default)
    {
        await this.gate.WaitAsync(cancellationToken);
        try
        {
            return await this.LoadUnlockedAsync(cancellationToken);
        }
        finally
        {
            this.gate.Release();
        }
    }

    public async Task SaveAsync(AppPreferences preferences, CancellationToken cancellationToken = default)
    {
        await this.gate.WaitAsync(cancellationToken);
        try
        {
            await this.SaveUnlockedAsync(preferences, cancellationToken);
        }
        finally
        {
            this.gate.Release();
        }
    }

    public async Task<AppPreferences> UpdateAsync(
        Func<AppPreferences, AppPreferences> update,
        CancellationToken cancellationToken = default)
    {
        await this.gate.WaitAsync(cancellationToken);
        try
        {
            var next = update(await this.LoadUnlockedAsync(cancellationToken));
            await this.SaveUnlockedAsync(next, cancellationToken);
            return next;
        }
        finally
        {
            this.gate.Release();
        }
    }

    private async Task<AppPreferences> LoadUnlockedAsync(CancellationToken cancellationToken)
    {
        var persisted = AppPreferences.Default;
        if (File.Exists(this.Path))
        {
            await using var stream = File.OpenRead(this.Path);
            persisted = (await JsonSerializer.DeserializeAsync<PersistedAppPreferences>(
                stream,
                JsonOptions,
                cancellationToken))?.ToAppPreferences() ?? AppPreferences.Default;
        }

        if (this.credentials is null)
        {
            return persisted;
        }

        return persisted with
        {
            GatewayToken = await this.credentials.LoadGatewayTokenAsync(cancellationToken),
            DeviceToken = await this.credentials.LoadDeviceTokenAsync(cancellationToken),
        };
    }

    private async Task SaveUnlockedAsync(AppPreferences preferences, CancellationToken cancellationToken)
    {
        if (this.credentials is not null)
        {
            await this.credentials.SaveGatewayTokenAsync(preferences.GatewayToken, cancellationToken);
            await this.credentials.SaveDeviceTokenAsync(preferences.DeviceToken, cancellationToken);
        }

        var directory = System.IO.Path.GetDirectoryName(this.Path)!;
        Directory.CreateDirectory(directory);
        var tempPath = System.IO.Path.Combine(
            directory,
            $"{System.IO.Path.GetFileName(this.Path)}.{Guid.NewGuid():N}.tmp");
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
                await JsonSerializer.SerializeAsync(
                    stream,
                    PersistedAppPreferences.From(preferences),
                    JsonOptions,
                    cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(tempPath, this.Path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private sealed record PersistedAppPreferences(
        bool OpenMainWindowOnLaunch,
        string GatewayUrl,
        string ChatSessionKey,
        bool VoiceControlsEnabled,
        bool GlobalHotkeyEnabled,
        string? LastStatus,
        DateTimeOffset? LastStatusCheckedAt,
        WindowsNotificationPreferences? NotificationPreferences)
    {
        public static PersistedAppPreferences From(AppPreferences preferences)
        {
            return new PersistedAppPreferences(
                preferences.OpenMainWindowOnLaunch,
                preferences.GatewayUrl,
                preferences.ChatSessionKey,
                preferences.VoiceControlsEnabled,
                preferences.GlobalHotkeyEnabled,
                preferences.LastStatus,
                preferences.LastStatusCheckedAt,
                preferences.NotificationPreferences);
        }

        public AppPreferences ToAppPreferences()
        {
            return new AppPreferences(
                this.OpenMainWindowOnLaunch,
                string.IsNullOrWhiteSpace(this.GatewayUrl) ? AppPreferences.Default.GatewayUrl : this.GatewayUrl,
                GatewayToken: null,
                DeviceToken: null,
                string.IsNullOrWhiteSpace(this.ChatSessionKey) ? AppPreferences.Default.ChatSessionKey : this.ChatSessionKey,
                this.VoiceControlsEnabled,
                this.GlobalHotkeyEnabled,
                this.LastStatus,
                this.LastStatusCheckedAt,
                this.NotificationPreferences ?? WindowsNotificationPreferences.Default);
        }
    }
}
