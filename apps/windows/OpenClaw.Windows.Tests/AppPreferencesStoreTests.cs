using System.Globalization;
using System.Text.Json;
using OpenClaw.Windows;

namespace OpenClaw.Windows.Tests;

[TestClass]
public sealed class AppPreferencesStoreTests
{
    [TestMethod]
    public async Task SavesAndLoadsPreferences()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "preferences.json");
        var store = new AppPreferencesStore(path);
        var expected = AppPreferences.Default with
        {
            OpenMainWindowOnLaunch = false,
            GatewayUrl = "ws://127.0.0.1:18800",
            ChatSessionKey = "windows",
            VoiceControlsEnabled = true,
            GlobalHotkeyEnabled = true,
            NotificationPreferences = new WindowsNotificationPreferences(
                ApprovalAlerts: false,
                PairingAlerts: true,
                GatewayHealthAlerts: false,
                DevicePermissionAlerts: true),
            LastStatus = "running",
            LastStatusCheckedAt = DateTimeOffset.Parse("2026-04-27T12:00:00Z", CultureInfo.InvariantCulture),
        };

        await store.SaveAsync(expected);
        var actual = await store.LoadAsync();

        Assert.AreEqual(expected.LastStatus, actual.LastStatus);
        Assert.AreEqual(expected.LastStatusCheckedAt, actual.LastStatusCheckedAt);
        Assert.AreEqual(expected.OpenMainWindowOnLaunch, actual.OpenMainWindowOnLaunch);
        Assert.AreEqual(expected.GatewayUrl, actual.GatewayUrl);
        Assert.AreEqual(expected.ChatSessionKey, actual.ChatSessionKey);
        Assert.AreEqual(expected.VoiceControlsEnabled, actual.VoiceControlsEnabled);
        Assert.AreEqual(expected.GlobalHotkeyEnabled, actual.GlobalHotkeyEnabled);
        Assert.AreEqual(expected.NotificationPreferences, actual.NotificationPreferences);
    }

    [TestMethod]
    public async Task SavesTokensToCredentialStoreInsteadOfPreferencesJson()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "preferences.json");
        var credentials = new InMemoryAppCredentialStore();
        var store = new AppPreferencesStore(path, credentials);

        await store.SaveAsync(AppPreferences.Default with
        {
            GatewayToken = "shared-token",
            DeviceToken = "device-token",
        });

        var raw = await File.ReadAllTextAsync(path);
        Assert.IsFalse(raw.Contains("shared-token", StringComparison.Ordinal));
        Assert.IsFalse(raw.Contains("device-token", StringComparison.Ordinal));
        Assert.AreEqual("shared-token", await credentials.LoadGatewayTokenAsync());
        Assert.AreEqual("device-token", await credentials.LoadDeviceTokenAsync());

        var actual = await store.LoadAsync();
        Assert.AreEqual("shared-token", actual.GatewayToken);
        Assert.AreEqual("device-token", actual.DeviceToken);
    }

    [TestMethod]
    public async Task UpdateAsyncSerializesConcurrentPreferenceWrites()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "preferences.json");
        var store = new AppPreferencesStore(path, new InMemoryAppCredentialStore());
        await store.SaveAsync(AppPreferences.Default with { LastStatus = "0" });

        var updates = Enumerable.Range(0, 20).Select(_ => store.UpdateAsync(current =>
        {
            var currentValue = int.Parse(current.LastStatus ?? "0", CultureInfo.InvariantCulture);
            return current with { LastStatus = (currentValue + 1).ToString(CultureInfo.InvariantCulture) };
        }));

        await Task.WhenAll(updates);

        var actual = await store.LoadAsync();
        Assert.AreEqual("20", actual.LastStatus);
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        Assert.AreEqual(JsonValueKind.Object, document.RootElement.ValueKind);
    }

}
