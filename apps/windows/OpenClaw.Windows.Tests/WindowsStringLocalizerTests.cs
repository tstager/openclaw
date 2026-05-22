namespace OpenClaw.Windows.Tests;

[TestClass]
public sealed class WindowsStringLocalizerTests
{
    [TestMethod]
    public void GetReturnsFallbackWhenKeyIsMissing()
    {
        var localizer = new WindowsStringLocalizer(_ => null);

        var value = localizer.Get("Shell.Navigation.Home", "Home");

        Assert.AreEqual("Home", value);
    }

    [TestMethod]
    public void FormatUsesLocalizedFormatString()
    {
        var localizer = new WindowsStringLocalizer(resourceKey => resourceKey switch
        {
            "Shell.Test.FormatMessage" => "Bonjour {0}",
            _ => null,
        });

        var value = localizer.Format("Shell.Test.FormatMessage", "Hello {0}", "Trent");

        Assert.AreEqual("Bonjour Trent", value);
    }
}
