using OpenClaw.Windows;

namespace OpenClaw.Windows.Tests;

[TestClass]
public sealed class WindowsActivationRouterTests
{
    [TestMethod]
    public void ParseLaunchArguments_ParsesChatSessionRoute()
    {
        var request = WindowsActivationRouter.ParseLaunchArguments("openclaw://chat?session=windows");

        Assert.IsNotNull(request);
        Assert.AreEqual(WindowsNavigationDestination.Chat, request.Destination);
        Assert.AreEqual("windows", request.ChatSessionKey);
    }

    [TestMethod]
    public void ParseLaunchArguments_MapsDiagnosticsToLogs()
    {
        var request = WindowsActivationRouter.ParseLaunchArguments("openclaw://diagnostics");

        Assert.IsNotNull(request);
        Assert.AreEqual(WindowsNavigationDestination.Logs, request.Destination);
    }
}
