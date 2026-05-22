using OpenClaw.Windows;

namespace OpenClaw.Windows.Tests;

[TestClass]
public sealed class WindowsActivityHistoryStoreTests
{
    [TestMethod]
    public async Task AddAsync_PersistsNewestEntriesWithinCapacity()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "activity-history.json");
        var store = new WindowsActivityHistoryStore(path);

        await store.AddAsync("gateway", "First", "one", WindowsNavigationDestination.Home, 2);
        await store.AddAsync("gateway", "Second", "two", WindowsNavigationDestination.Home, 2);
        await store.AddAsync("gateway", "Third", "three", WindowsNavigationDestination.Home, 2);

        var reloaded = new WindowsActivityHistoryStore(path);
        Assert.HasCount(2, reloaded.Entries);
        Assert.AreEqual("Third", reloaded.Entries[0].Title);
        Assert.AreEqual("Second", reloaded.Entries[1].Title);
    }
}
