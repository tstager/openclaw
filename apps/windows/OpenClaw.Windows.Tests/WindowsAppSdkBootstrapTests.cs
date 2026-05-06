using Microsoft.UI.Xaml;
using OpenClaw.Windows;

namespace OpenClaw.Windows.Tests;

[TestClass]
public sealed class WindowsAppSdkBootstrapTests
{
    [TestMethod]
    public void TestHostLoadsWinUiApplicationType()
    {
        var appType = typeof(App);
        var baseType = appType.BaseType;

        Assert.AreEqual("OpenClaw.Windows.App", appType.FullName);
        Assert.IsNotNull(baseType);
        Assert.AreSame(typeof(Application), baseType);
        Assert.AreEqual("Microsoft.UI.Xaml.Application", baseType.FullName);
    }
}
