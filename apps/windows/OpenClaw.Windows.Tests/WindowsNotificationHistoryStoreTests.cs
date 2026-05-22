using OpenClaw.Windows;

namespace OpenClaw.Windows.Tests;

[TestClass]
public sealed class WindowsNotificationHistoryStoreTests
{
    [TestMethod]
    public async Task AddAsync_PersistsNewestEntriesWithinCapacity()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "notification-history.json");
        var store = new WindowsNotificationHistoryStore(path);

        await store.AddAsync(
            WindowsNavigationDestination.Approvals,
            "First",
            "one",
            capacity: 2,
            category: WindowsNotificationCategories.Operator,
            kind: WindowsNotificationKind.Approval);
        await store.AddAsync(
            WindowsNavigationDestination.Pairing,
            "Second",
            "two",
            capacity: 2,
            category: WindowsNotificationCategories.Operator,
            kind: WindowsNotificationKind.Pairing);
        await store.AddAsync(
            WindowsNavigationDestination.Devices,
            "Third",
            "three",
            capacity: 2,
            category: WindowsNotificationCategories.Device,
            kind: WindowsNotificationKind.DevicePermission);

        var reloaded = new WindowsNotificationHistoryStore(path);
        Assert.HasCount(2, reloaded.Entries);
        Assert.AreEqual("Third", reloaded.Entries[0].Title);
        Assert.AreEqual(WindowsNotificationKind.DevicePermission, reloaded.Entries[0].Kind);
        Assert.AreEqual(WindowsNotificationCategories.Device, reloaded.Entries[0].Category);
        Assert.AreEqual("Second", reloaded.Entries[1].Title);
    }

    [TestMethod]
    public async Task ClearAsync_RemovesPersistedEntries()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "notification-history.json");
        var store = new WindowsNotificationHistoryStore(path);
        await store.AddAsync(WindowsNavigationDestination.Home, "Entry", "one", capacity: 5);

        await store.ClearAsync();

        var reloaded = new WindowsNotificationHistoryStore(path);
        Assert.HasCount(0, reloaded.Entries);
        Assert.IsNull(reloaded.Latest);
    }
}
