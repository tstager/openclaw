using System.Globalization;
using System.Text.Json;
using OpenClaw.Windows;
using Windows.UI;

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
            ThemePreference = WindowsThemePreference.Dark,
            AccentColorPreference = WindowsAccentColorPreference.Purple,
            CustomAccentColor = Color.FromArgb(255, 18, 52, 86),
            ColorThemePreference = WindowsColorThemePreference.Forest,
            CustomColorTheme = Color.FromArgb(255, 120, 40, 200),
            VoiceControlsEnabled = true,
            GlobalHotkeyEnabled = true,
            NotificationPreferences = new WindowsNotificationPreferences(
                ApprovalAlerts: false,
                PairingAlerts: true,
                GatewayHealthAlerts: false,
                DevicePermissionAlerts: true),
            NotificationRules = new WindowsNotificationRulePreferences(
                HistoryRetentionCount: 150,
                Rules:
                [
                    new WindowsNotificationRule(
                        "approval-triage",
                        WindowsNotificationKind.Approval,
                        "triage",
                        WindowsNavigationDestination.Approvals,
                        true),
                    new WindowsNotificationRule(
                        "gateway-status",
                        WindowsNotificationKind.GatewayHealth,
                        WindowsNotificationCategories.Gateway,
                        WindowsNavigationDestination.Logs,
                        false),
                ]),
            Topology = new WindowsTopologyPreferences(
                AutoStartTunnel: true,
                SshHost: "trent@example.com",
                RemoteHost: "127.0.0.1",
                LocalPort: 28789,
                RemotePort: 18789),
            Diagnostics = new WindowsDiagnosticsPreferences(
                StructuredDiagnosticsEnabled: true,
                StructuredDiagnosticsPath: @"C:\logs\windows.jsonl",
                ActivityRetentionCount: 250),
            Policy = new WindowsPolicyPreferences(
                ApprovalPolicy: WindowsApprovalPolicyPreference.AllowSafeCommands,
                BlockUnsafeUrls: true,
                RedactSensitiveContent: true,
                RememberedAllowedCommands: ["pnpm test"]),
            LastStatus = "running",
            LastStatusCheckedAt = DateTimeOffset.Parse("2026-04-27T12:00:00Z", CultureInfo.InvariantCulture),
            SessionEventVisibility = SessionEventVisibility.ChatOnly(AppPreferences.Default.SessionEventVisibility)
                .WithEventType("custom.event", false),
        };

        await store.SaveAsync(expected);
        var actual = await store.LoadAsync();

        Assert.AreEqual(expected.LastStatus, actual.LastStatus);
        Assert.AreEqual(expected.LastStatusCheckedAt, actual.LastStatusCheckedAt);
        Assert.AreEqual(expected.OpenMainWindowOnLaunch, actual.OpenMainWindowOnLaunch);
        Assert.AreEqual(expected.GatewayUrl, actual.GatewayUrl);
        Assert.AreEqual(expected.ChatSessionKey, actual.ChatSessionKey);
        Assert.AreEqual(expected.ThemePreference, actual.ThemePreference);
        Assert.AreEqual(expected.AccentColorPreference, actual.AccentColorPreference);
        Assert.AreEqual(expected.CustomAccentColor, actual.CustomAccentColor);
        Assert.AreEqual(expected.ColorThemePreference, actual.ColorThemePreference);
        Assert.AreEqual(expected.CustomColorTheme, actual.CustomColorTheme);
        Assert.AreEqual(expected.VoiceControlsEnabled, actual.VoiceControlsEnabled);
        Assert.AreEqual(expected.GlobalHotkeyEnabled, actual.GlobalHotkeyEnabled);
        Assert.AreEqual(expected.NotificationPreferences, actual.NotificationPreferences);
        Assert.AreEqual(expected.NotificationRules.HistoryRetentionCount, actual.NotificationRules.HistoryRetentionCount);
        Assert.HasCount(2, actual.NotificationRules.Rules);
        Assert.AreEqual(expected.NotificationRules.Rules[0], actual.NotificationRules.Rules[0]);
        Assert.AreEqual(expected.NotificationRules.Rules[1], actual.NotificationRules.Rules[1]);
        Assert.AreEqual(expected.Topology, actual.Topology);
        Assert.AreEqual(expected.Diagnostics, actual.Diagnostics);
        CollectionAssert.AreEqual(
            expected.Policy.RememberedAllowedCommands.ToArray(),
            actual.Policy.RememberedAllowedCommands.ToArray());
        Assert.AreEqual(expected.Policy.ApprovalPolicy, actual.Policy.ApprovalPolicy);
        Assert.AreEqual(expected.Policy.BlockUnsafeUrls, actual.Policy.BlockUnsafeUrls);
        Assert.AreEqual(expected.Policy.RedactSensitiveContent, actual.Policy.RedactSensitiveContent);
        Assert.IsFalse(actual.SessionEventVisibility.IsVisible("tick"));
        Assert.IsFalse(actual.SessionEventVisibility.IsVisible("custom.event"));
        Assert.IsTrue(actual.SessionEventVisibility.IsVisible("chat"));
        Assert.AreEqual(SessionEventVisibilityPreset.Custom, actual.SessionEventVisibility.Preset);
    }

    [TestMethod]
    public async Task SavesAndLoadsCustomAppearanceColors()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "preferences.json");
        var store = new AppPreferencesStore(path);

        await store.SaveAsync(AppPreferences.Default with
        {
            AccentColorPreference = WindowsAccentColorPreference.Custom,
            CustomAccentColor = Color.FromArgb(255, 10, 120, 210),
            ColorThemePreference = WindowsColorThemePreference.Custom,
            CustomColorTheme = Color.FromArgb(255, 170, 50, 120),
        });

        var actual = await store.LoadAsync();
        var raw = await File.ReadAllTextAsync(path);

        Assert.AreEqual(WindowsAccentColorPreference.Custom, actual.AccentColorPreference);
        Assert.AreEqual(Color.FromArgb(255, 10, 120, 210), actual.CustomAccentColor);
        Assert.AreEqual(WindowsColorThemePreference.Custom, actual.ColorThemePreference);
        Assert.AreEqual(Color.FromArgb(255, 170, 50, 120), actual.CustomColorTheme);
        Assert.IsTrue(raw.Contains("\"customAccentColor\": \"#0A78D2\"", StringComparison.Ordinal));
        Assert.IsTrue(raw.Contains("\"customColorTheme\": \"#AA3278\"", StringComparison.Ordinal));
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

    [TestMethod]
    public async Task MissingThemePreferenceDefaultsToSystem()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "preferences.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(
            path,
            """
            {
              "openMainWindowOnLaunch": true,
              "gatewayUrl": "ws://127.0.0.1:18789",
              "chatSessionKey": "main",
              "voiceControlsEnabled": false,
              "globalHotkeyEnabled": false
            }
            """);
        var store = new AppPreferencesStore(path);

        var actual = await store.LoadAsync();

        Assert.AreEqual(WindowsThemePreference.System, actual.ThemePreference);
    }

    [TestMethod]
    public async Task MissingAccentColorPreferenceDefaultsToSystem()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "preferences.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(
            path,
            """
            {
              "openMainWindowOnLaunch": true,
              "gatewayUrl": "ws://127.0.0.1:18789",
              "chatSessionKey": "main",
              "theme": "Dark",
              "voiceControlsEnabled": false,
              "globalHotkeyEnabled": false
            }
            """);
        var store = new AppPreferencesStore(path);

        var actual = await store.LoadAsync();

        Assert.AreEqual(WindowsAccentColorPreference.System, actual.AccentColorPreference);
    }

    [TestMethod]
    public async Task MissingColorThemePreferenceDefaultsToDefault()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "preferences.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(
            path,
            """
            {
              "openMainWindowOnLaunch": true,
              "gatewayUrl": "ws://127.0.0.1:18789",
              "chatSessionKey": "main",
              "theme": "Dark",
              "accentColor": "Purple",
              "voiceControlsEnabled": false,
              "globalHotkeyEnabled": false
            }
            """);
        var store = new AppPreferencesStore(path);

        var actual = await store.LoadAsync();

        Assert.AreEqual(WindowsColorThemePreference.Default, actual.ColorThemePreference);
    }

    [TestMethod]
    public async Task MissingSessionEventVisibilityDefaultsToAllVisible()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "preferences.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(
            path,
            """
            {
              "openMainWindowOnLaunch": true,
              "gatewayUrl": "ws://127.0.0.1:18789",
              "chatSessionKey": "main",
              "theme": "Dark",
              "voiceControlsEnabled": false,
              "globalHotkeyEnabled": false
            }
            """);
        var store = new AppPreferencesStore(path);

        var actual = await store.LoadAsync();

        Assert.IsTrue(actual.SessionEventVisibility.IsVisible("tick"));
        Assert.IsTrue(actual.SessionEventVisibility.IsVisible("chat"));
    }

    [TestMethod]
    public async Task SavesAndLoadsSessionEventVisibilityPreset()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "preferences.json");
        var store = new AppPreferencesStore(path);

        await store.SaveAsync(AppPreferences.Default with
        {
            SessionEventVisibility = SessionEventVisibility.ChatOnly(AppPreferences.Default.SessionEventVisibility),
        });

        var actual = await store.LoadAsync();

        Assert.AreEqual(SessionEventVisibilityPreset.ChatOnly, actual.SessionEventVisibility.Preset);
        Assert.IsTrue(actual.SessionEventVisibility.IsVisible("chat"));
        Assert.IsFalse(actual.SessionEventVisibility.IsVisible("tick"));
    }

    [TestMethod]
    public async Task UnknownAccentColorPreferenceDefaultsToSystem()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "preferences.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(
            path,
            """
            {
              "openMainWindowOnLaunch": true,
              "gatewayUrl": "ws://127.0.0.1:18789",
              "chatSessionKey": "main",
              "theme": "Dark",
              "accentColor": "Neon",
              "voiceControlsEnabled": false,
              "globalHotkeyEnabled": false
            }
            """);
        var store = new AppPreferencesStore(path);

        var actual = await store.LoadAsync();

        Assert.AreEqual(WindowsAccentColorPreference.System, actual.AccentColorPreference);
    }

    [TestMethod]
    public async Task UnknownThemePreferenceDefaultsToSystem()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "preferences.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(
            path,
            """
            {
              "openMainWindowOnLaunch": true,
              "gatewayUrl": "ws://127.0.0.1:18789",
              "chatSessionKey": "main",
              "theme": "Solarized",
              "voiceControlsEnabled": false,
              "globalHotkeyEnabled": false
            }
            """);
        var store = new AppPreferencesStore(path);

        var actual = await store.LoadAsync();

        Assert.AreEqual(WindowsThemePreference.System, actual.ThemePreference);
    }

    [TestMethod]
    public async Task UnknownColorThemePreferenceDefaultsToDefault()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "preferences.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(
            path,
            """
            {
              "openMainWindowOnLaunch": true,
              "gatewayUrl": "ws://127.0.0.1:18789",
              "chatSessionKey": "main",
              "theme": "Dark",
              "accentColor": "Purple",
              "colorTheme": "Solarized",
              "voiceControlsEnabled": false,
              "globalHotkeyEnabled": false
            }
            """);
        var store = new AppPreferencesStore(path);

        var actual = await store.LoadAsync();

        Assert.AreEqual(WindowsColorThemePreference.Default, actual.ColorThemePreference);
    }

    [TestMethod]
    public async Task InvalidCustomAppearanceColorsNormalizeToDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "preferences.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(
            path,
            """
            {
              "openMainWindowOnLaunch": true,
              "gatewayUrl": "ws://127.0.0.1:18789",
              "chatSessionKey": "main",
              "theme": "Dark",
              "accentColor": "Custom",
              "customAccentColor": "not-a-color",
              "colorTheme": "Custom",
              "customColorTheme": "#12345Z",
              "voiceControlsEnabled": false,
              "globalHotkeyEnabled": false
            }
            """);
        var store = new AppPreferencesStore(path);

        var actual = await store.LoadAsync();

        Assert.AreEqual(WindowsAccentColorPreference.System, actual.AccentColorPreference);
        Assert.IsNull(actual.CustomAccentColor);
        Assert.AreEqual(WindowsColorThemePreference.Default, actual.ColorThemePreference);
        Assert.IsNull(actual.CustomColorTheme);
    }


    [TestMethod]
    public async Task MissingNestedPreferencesDefaultToCurrentDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "preferences.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(
            path,
            """
            {
              "openMainWindowOnLaunch": true,
              "gatewayUrl": "ws://127.0.0.1:18789",
              "chatSessionKey": "main",
              "theme": "Dark",
              "accentColor": "Purple",
              "colorTheme": "Forest",
              "voiceControlsEnabled": false,
              "globalHotkeyEnabled": false
            }
            """);
        var store = new AppPreferencesStore(path);

        var actual = await store.LoadAsync();

        Assert.AreEqual(WindowsTopologyPreferences.Default, actual.Topology);
        Assert.AreEqual(WindowsDiagnosticsPreferences.Default, actual.Diagnostics);
        Assert.AreEqual(
            WindowsNotificationRulePreferences.Default.HistoryRetentionCount,
            actual.NotificationRules.HistoryRetentionCount);
        CollectionAssert.AreEqual(
            WindowsNotificationRulePreferences.Default.Rules.ToArray(),
            actual.NotificationRules.Rules.ToArray());
        Assert.AreEqual(WindowsPolicyPreferences.Default.ApprovalPolicy, actual.Policy.ApprovalPolicy);
        Assert.IsTrue(actual.Policy.BlockUnsafeUrls);
        Assert.IsTrue(actual.Policy.RedactSensitiveContent);
        Assert.IsEmpty(actual.Policy.RememberedAllowedCommands);
    }

    [TestMethod]
    public async Task InvalidNotificationRulesNormalizeToSupportedDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "preferences.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(
            path,
            """
            {
              "openMainWindowOnLaunch": true,
              "gatewayUrl": "ws://127.0.0.1:18789",
              "chatSessionKey": "main",
              "voiceControlsEnabled": false,
              "globalHotkeyEnabled": false,
              "notificationRules": {
                "historyRetentionCount": 0,
                "rules": [
                  {
                    "id": " approval ",
                    "kind": "Approval",
                    "category": " Operator ",
                    "destination": " approvals ",
                    "enabled": true
                  },
                  {
                    "id": "approval",
                    "kind": "Nope",
                    "category": "",
                    "destination": "",
                    "enabled": false
                  }
                ]
              }
            }
            """);
        var store = new AppPreferencesStore(path);

        var actual = await store.LoadAsync();

        Assert.AreEqual(WindowsNotificationRulePreferences.Default.HistoryRetentionCount, actual.NotificationRules.HistoryRetentionCount);
        Assert.HasCount(1, actual.NotificationRules.Rules);
        Assert.AreEqual("approval", actual.NotificationRules.Rules[0].Id);
        Assert.AreEqual(WindowsNotificationCategories.Operator, actual.NotificationRules.Rules[0].Category);
        Assert.AreEqual(WindowsNavigationDestination.Approvals, actual.NotificationRules.Rules[0].Destination);
    }

}
