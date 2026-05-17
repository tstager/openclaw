using System.Text.Json;

namespace OpenClaw.Windows;

/// <summary>
/// User-selected theme behavior for the WinUI shell.
/// </summary>
public enum WindowsThemePreference
{
    System,
    Light,
    Dark,
}

/// <summary>
/// Non-secret app settings persisted between Windows companion sessions.
/// </summary>
public sealed record AppPreferences(
    bool OpenMainWindowOnLaunch,
    string GatewayUrl,
    string? GatewayToken,
    string? DeviceToken,
    string ChatSessionKey,
    WindowsThemePreference ThemePreference,
    bool VoiceControlsEnabled,
    bool GlobalHotkeyEnabled,
    string? LastStatus,
    DateTimeOffset? LastStatusCheckedAt,
    SessionEventVisibilityPreferences SessionEventVisibility,
    WindowsNotificationPreferences NotificationPreferences)
{
    /// <summary>
    /// Defaults used for a fresh install and for missing/invalid persisted fields.
    /// </summary>
    public static AppPreferences Default { get; } = new(
        OpenMainWindowOnLaunch: true,
        GatewayUrl: "ws://127.0.0.1:18789",
        GatewayToken: null,
        DeviceToken: null,
        ChatSessionKey: "main",
        ThemePreference: WindowsThemePreference.System,
        VoiceControlsEnabled: false,
        GlobalHotkeyEnabled: false,
        LastStatus: null,
        LastStatusCheckedAt: null,
        SessionEventVisibility: SessionEventVisibilityPreferences.Default,
        NotificationPreferences: WindowsNotificationPreferences.Default);
}

/// <summary>
/// Persists preferences as JSON while delegating tokens and private keys to the credential store.
/// </summary>
public sealed class AppPreferencesStore : IDisposable
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

    /// <summary>
    /// Creates the production store under LocalAppData/OpenClaw/WindowsCompanion.
    /// </summary>
    public static AppPreferencesStore CreateDefault(IAppCredentialStore credentials)
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return new AppPreferencesStore(
            System.IO.Path.Combine(root, "OpenClaw", "WindowsCompanion", "preferences.json"),
            credentials);
    }

    /// <summary>
    /// Loads preferences and merges secrets from the credential store when one is configured.
    /// </summary>
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

    /// <summary>
    /// Writes the full preference snapshot using a temp file and atomic replacement.
    /// </summary>
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

    /// <summary>
    /// Serializes read-modify-write updates so overlapping UI actions do not corrupt preferences.
    /// </summary>
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

    public void Dispose()
    {
        this.gate.Dispose();
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

    /// <summary>
    /// JSON-only representation. Secret values are intentionally absent from this record.
    /// </summary>
    private sealed record PersistedAppPreferences(
        bool OpenMainWindowOnLaunch,
        string GatewayUrl,
        string ChatSessionKey,
        string? Theme,
        bool VoiceControlsEnabled,
        bool GlobalHotkeyEnabled,
        string? LastStatus,
        DateTimeOffset? LastStatusCheckedAt,
        Dictionary<string, bool>? SessionEventVisibility,
        string? SessionEventVisibilityPreset,
        WindowsNotificationPreferences? NotificationPreferences)
    {
        public static PersistedAppPreferences From(AppPreferences preferences)
        {
            return new PersistedAppPreferences(
                preferences.OpenMainWindowOnLaunch,
                preferences.GatewayUrl,
                preferences.ChatSessionKey,
                preferences.ThemePreference.ToString(),
                preferences.VoiceControlsEnabled,
                preferences.GlobalHotkeyEnabled,
                preferences.LastStatus,
                preferences.LastStatusCheckedAt,
                preferences.SessionEventVisibility.EventTypes.ToDictionary(
                    static entry => entry.Key,
                    static entry => entry.Value,
                    StringComparer.Ordinal),
                preferences.SessionEventVisibility.Preset.ToString(),
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
                ParseThemePreference(this.Theme),
                this.VoiceControlsEnabled,
                this.GlobalHotkeyEnabled,
                this.LastStatus,
                this.LastStatusCheckedAt,
                SessionEventVisibilityPreferences.From(
                    this.SessionEventVisibility,
                    ParseSessionEventVisibilityPreset(this.SessionEventVisibilityPreset)),
                this.NotificationPreferences ?? WindowsNotificationPreferences.Default);
        }

        private static WindowsThemePreference ParseThemePreference(string? value)
        {
            return Enum.TryParse<WindowsThemePreference>(value, ignoreCase: true, out var theme)
                ? theme
                : AppPreferences.Default.ThemePreference;
        }

        private static SessionEventVisibilityPreset ParseSessionEventVisibilityPreset(string? value)
        {
            return Enum.TryParse<SessionEventVisibilityPreset>(value, ignoreCase: true, out var preset)
                ? preset
                : AppPreferences.Default.SessionEventVisibility.Preset;
        }
    }
}
