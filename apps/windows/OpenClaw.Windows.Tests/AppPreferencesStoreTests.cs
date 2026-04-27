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
            LastStatus = "running",
            LastStatusCheckedAt = DateTimeOffset.Parse("2026-04-27T12:00:00Z"),
        };

        await store.SaveAsync(expected);
        var actual = await store.LoadAsync();

        Assert.AreEqual(expected.LastStatus, actual.LastStatus);
        Assert.AreEqual(expected.LastStatusCheckedAt, actual.LastStatusCheckedAt);
    }
}
