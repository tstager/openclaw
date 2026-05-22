namespace OpenClaw.Windows.Tests;

[TestClass]
public sealed class WindowsNavigationServiceTests
{
    [TestMethod]
    public void PrimaryItemsUseLocalizedLabels()
    {
        var localizer = new WindowsStringLocalizer(resourceKey => resourceKey switch
        {
            "Shell.Navigation.Home" => "Accueil",
            "Shell.Navigation.Chat" => "Discussion",
            "Shell.Navigation.Canvas" => "Canvas",
            "Shell.Navigation.Sessions" => "Sessions",
            "Shell.Navigation.Approvals" => "Approbations",
            "Shell.Navigation.Pairing" => "Jumelage",
            "Shell.Navigation.Devices" => "Appareils",
            "Shell.Navigation.Logs" => "Journaux",
            "Shell.Navigation.Settings" => "Paramètres",
            _ => null,
        });
        var navigation = new WindowsNavigationService(localizer);

        CollectionAssert.AreEqual(
            new[] { "Accueil", "Discussion", "Canvas", "Sessions", "Approbations", "Jumelage", "Appareils", "Journaux" },
            navigation.PrimaryItems.Select(item => item.Label).ToArray());
        Assert.AreEqual("Paramètres", navigation.PageTitle(WindowsNavigationDestination.Settings));
    }
}
