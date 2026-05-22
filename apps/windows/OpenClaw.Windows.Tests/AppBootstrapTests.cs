using OpenClaw.Protocol.Generated;
using OpenClaw.Windows;

namespace OpenClaw.Windows.Tests;

[TestClass]
public sealed class AppBootstrapTests
{
    [TestMethod]
    public void StartupSummaryUsesGeneratedProtocolVersion()
    {
        var summary = AppBootstrap.CreateStartupSummary(new WindowsStringLocalizer(_ => null));

        Assert.AreEqual("OpenClaw", summary.AppName);
        Assert.AreEqual(GatewayProtocol.Version, summary.GatewayProtocolVersion);
        Assert.IsTrue(summary.HostCapabilities.SupportsTray);
        Assert.IsTrue(summary.HostCapabilities.SupportsOverlays);
    }

    [TestMethod]
    public void StartupSummaryUsesLocalizedAppNameWhenAvailable()
    {
        var localizer = new WindowsStringLocalizer(resourceKey => resourceKey switch
        {
            "Shell.AppTitle" => "OpenClaw FR",
            _ => null,
        });

        var summary = AppBootstrap.CreateStartupSummary(localizer);

        Assert.AreEqual("OpenClaw FR", summary.AppName);
        Assert.AreEqual(GatewayProtocol.Version, summary.GatewayProtocolVersion);
    }
}
