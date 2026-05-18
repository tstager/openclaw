using OpenClaw.Windows;

namespace OpenClaw.Windows.Tests;

[TestClass]
public sealed class WindowsNavigationTests
{
    [TestMethod]
    public void Normalize_KnownDiagnosticsRoute_MapsToLogs()
    {
        var destination = WindowsNavigationService.Normalize(WindowsNavigationDestination.Diagnostics);

        Assert.AreEqual(WindowsNavigationDestination.Logs, destination);
    }

    [TestMethod]
    public void PrimaryItems_ExposeExpectedShellDestinations()
    {
        var navigation = new WindowsNavigationService();
        var destinations = navigation.PrimaryItems.Select(item => item.Destination).ToArray();

        CollectionAssert.AreEqual(
            new[]
            {
                WindowsNavigationDestination.Home,
                WindowsNavigationDestination.Chat,
                WindowsNavigationDestination.Canvas,
                WindowsNavigationDestination.Sessions,
                WindowsNavigationDestination.Approvals,
                WindowsNavigationDestination.Pairing,
                WindowsNavigationDestination.Devices,
                WindowsNavigationDestination.Logs,
            },
            destinations);
    }
}
