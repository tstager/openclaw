using OpenClaw.Windows;

namespace OpenClaw.Windows.Tests;

[TestClass]
public sealed class WindowsNotificationActivityTests
{
    [TestMethod]
    public void Add_RecordsLatestNotificationFirst()
    {
        var log = new WindowsNotificationActivityLog();

        log.Add(WindowsNavigationDestination.Approvals, "Approval", "One pending.");
        log.Add(WindowsNavigationDestination.Pairing, "Pairing", "Two pending.");

        Assert.AreEqual("Pairing", log.Latest?.Title);
        Assert.AreEqual(WindowsNavigationDestination.Pairing, log.Entries[0].Destination);
        Assert.AreEqual(WindowsNavigationDestination.Approvals, log.Entries[1].Destination);
    }

    [TestMethod]
    public void Add_TrimsEntriesToCapacity()
    {
        var log = new WindowsNotificationActivityLog(capacity: 2);

        log.Add(WindowsNavigationDestination.Home, "First", "first");
        log.Add(WindowsNavigationDestination.Home, "Second", "second");
        log.Add(WindowsNavigationDestination.Home, "Third", "third");

        Assert.HasCount(2, log.Entries);
        Assert.AreEqual("Third", log.Entries[0].Title);
        Assert.AreEqual("Second", log.Entries[1].Title);
    }
}
