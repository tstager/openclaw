using OpenClaw.Windows;

namespace OpenClaw.Windows.Tests;

[TestClass]
public sealed class GatewayCliCommandRunnerTests
{
    [TestMethod]
    public void SourceCheckoutRunnerUsesOpenClawMjsFromAncestor()
    {
        var root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            var nested = Path.Combine(root, "apps", "windows", "OpenClaw.Windows", "bin");
            Directory.CreateDirectory(nested);
            File.WriteAllText(Path.Combine(root, "package.json"), """{"name":"openclaw"}""");
            File.WriteAllText(Path.Combine(root, "openclaw.mjs"), "");

            var runner = GatewayCliCommandRunner.TryCreateFromSourceCheckout(nested);

            Assert.IsNotNull(runner);
            Assert.AreEqual("node", runner.Executable);
            CollectionAssert.AreEqual(new[] { Path.Combine(root, "openclaw.mjs") }, runner.BaseArguments.ToArray());
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    public void SourceCheckoutRunnerReturnsNullOutsideRepo()
    {
        var root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            Directory.CreateDirectory(root);

            var runner = GatewayCliCommandRunner.TryCreateFromSourceCheckout(root);

            Assert.IsNull(runner);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
