using OpenClaw.Windows;

namespace OpenClaw.Windows.Tests;

[TestClass]
[DoNotParallelize]
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
            Directory.CreateDirectory(Path.Combine(root, "dist"));
            File.WriteAllText(Path.Combine(root, "package.json"), """{"name":"openclaw"}""");
            File.WriteAllText(Path.Combine(root, "openclaw.mjs"), "");
            File.WriteAllText(Path.Combine(root, "dist", "entry.mjs"), "");

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
    public void SourceCheckoutRunnerReturnsNullForUnbuiltSourceCheckout()
    {
        var root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "package.json"), """{"name":"openclaw"}""");
            File.WriteAllText(Path.Combine(root, "openclaw.mjs"), "");

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

    [TestMethod]
    public void ResolveExecutablePathUsesPathExtCommandShim()
    {
        var root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            Directory.CreateDirectory(root);
            var shim = Path.Combine(root, "openclaw.cmd");
            File.WriteAllText(shim, "");

            var executable = GatewayCliCommandRunner.ResolveExecutablePath(
                "openclaw",
                root,
                ".COM;.EXE;.BAT;.CMD");

            Assert.AreEqual(shim, executable);
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
    public void CreateGlobalOpenClawRunnerRunsCommandShimThroughCmd()
    {
        var root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var originalPath = Environment.GetEnvironmentVariable("PATH");
        try
        {
            Directory.CreateDirectory(root);
            var shim = Path.Combine(root, "openclaw.cmd");
            File.WriteAllText(shim, "");
            Environment.SetEnvironmentVariable("PATH", root);

            var runner = GatewayCliCommandRunner.CreateGlobalOpenClawRunner();

            StringAssert.EndsWith(runner.Executable, "cmd.exe");
            CollectionAssert.AreEqual(new[] { "/d", "/c", shim }, runner.BaseArguments.ToArray());
            Assert.AreEqual("openclaw", runner.CommandName);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    public void ResolveExecutablePathExpandsEnvironmentVariablesFromPath()
    {
        var root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var originalValue = Environment.GetEnvironmentVariable("OPENCLAW_TEST_NPM_PREFIX");
        try
        {
            Directory.CreateDirectory(root);
            var shim = Path.Combine(root, "openclaw.cmd");
            File.WriteAllText(shim, "");
            Environment.SetEnvironmentVariable("OPENCLAW_TEST_NPM_PREFIX", root);

            var executable = GatewayCliCommandRunner.ResolveExecutablePath(
                "openclaw",
                @"%OPENCLAW_TEST_NPM_PREFIX%",
                ".COM;.EXE;.BAT;.CMD");

            Assert.AreEqual(shim, executable);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENCLAW_TEST_NPM_PREFIX", originalValue);
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    public void ResolveExecutablePathUsesNpmConfigPrefixEnvironmentVariable()
    {
        var root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var originalPrefix = Environment.GetEnvironmentVariable("NPM_CONFIG_PREFIX");
        var originalLowerPrefix = Environment.GetEnvironmentVariable("npm_config_prefix");
        try
        {
            Directory.CreateDirectory(root);
            var commandName = $"openclaw-prefix-test-{Guid.NewGuid():N}";
            var shim = Path.Combine(root, $"{commandName}.cmd");
            File.WriteAllText(shim, "");
            Environment.SetEnvironmentVariable("NPM_CONFIG_PREFIX", root);
            Environment.SetEnvironmentVariable("npm_config_prefix", root);

            var executable = GatewayCliCommandRunner.ResolveExecutablePath(
                commandName,
                pathVariable: "",
                pathExtVariable: ".COM;.EXE;.BAT;.CMD");

            Assert.AreEqual(shim, executable);
        }
        finally
        {
            Environment.SetEnvironmentVariable("NPM_CONFIG_PREFIX", originalPrefix);
            Environment.SetEnvironmentVariable("npm_config_prefix", originalLowerPrefix);
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task MissingExecutableReturnsFailedResult()
    {
        var runner = new GatewayCliCommandRunner($"openclaw-missing-for-test-{Guid.NewGuid():N}");

        var result = await runner.RunAsync(["--version"]);

        Assert.IsFalse(result.Succeeded);
        StringAssert.Contains(result.CombinedOutput, "OpenClaw CLI was not found");
        StringAssert.Contains(result.CombinedOutput, "The Windows app looked for:");
        StringAssert.Contains(result.CombinedOutput, "Searched locations:");
        StringAssert.Contains(result.CombinedOutput, "Detected:");
        StringAssert.Contains(result.CombinedOutput, "expected shim exists:");
        StringAssert.Contains(result.CombinedOutput, "npm install -g openclaw");
    }

    [TestMethod]
    public void ResolutionDiagnosticsReportsExpectedNpmShim()
    {
        var root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var originalPrefix = Environment.GetEnvironmentVariable("NPM_CONFIG_PREFIX");
        var originalLowerPrefix = Environment.GetEnvironmentVariable("npm_config_prefix");
        try
        {
            Directory.CreateDirectory(root);
            var shim = Path.Combine(root, "openclaw.cmd");
            File.WriteAllText(shim, "");
            Environment.SetEnvironmentVariable("NPM_CONFIG_PREFIX", root);
            Environment.SetEnvironmentVariable("npm_config_prefix", root);

            var diagnostics = GatewayCliCommandRunner.CreateResolutionDiagnostics("openclaw");

            Assert.IsTrue(string.Equals(root, diagnostics.NpmPrefix, StringComparison.Ordinal));
            Assert.IsTrue(string.Equals(shim, diagnostics.ExpectedNpmShim, StringComparison.Ordinal));
            Assert.IsTrue(diagnostics.ExpectedNpmShimExists);
            CollectionAssert.Contains(diagnostics.CandidateNames.ToArray(), "openclaw.cmd");
        }
        finally
        {
            Environment.SetEnvironmentVariable("NPM_CONFIG_PREFIX", originalPrefix);
            Environment.SetEnvironmentVariable("npm_config_prefix", originalLowerPrefix);
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
