using OpenClaw.Windows;

namespace OpenClaw.Windows.Tests;

[TestClass]
public sealed class WindowsUrlRiskEvaluatorTests
{
    [TestMethod]
    public void Evaluate_AllowsHttps()
    {
        var result = new WindowsUrlRiskEvaluator().Evaluate("https://docs.openclaw.ai/");

        Assert.IsTrue(result.Allowed);
        Assert.AreEqual("https://docs.openclaw.ai/", result.NormalizedUrl);
    }

    [TestMethod]
    public void Evaluate_BlocksFileScheme()
    {
        var result = new WindowsUrlRiskEvaluator().Evaluate("file:///C:/secret.txt");

        Assert.IsFalse(result.Allowed);
        StringAssert.Contains(result.Reason, "blocked");
    }
}
