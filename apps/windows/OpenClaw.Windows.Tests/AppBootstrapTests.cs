using OpenClaw.Protocol.Generated;
using OpenClaw.Windows;

namespace OpenClaw.Windows.Tests;

[TestClass]
public sealed class AppBootstrapTests
{
    [TestMethod]
    public void StartupSummaryUsesGeneratedProtocolVersion()
    {
        var summary = AppBootstrap.CreateStartupSummary();

        Assert.AreEqual("OpenClaw", summary.AppName);
        Assert.AreEqual(GatewayProtocol.Version, summary.GatewayProtocolVersion);
        Assert.IsTrue(summary.HostCapabilities.SupportsTray);
    }
}
