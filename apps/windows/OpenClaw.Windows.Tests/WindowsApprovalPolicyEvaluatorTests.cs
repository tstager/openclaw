using OpenClaw.Windows;

namespace OpenClaw.Windows.Tests;

[TestClass]
public sealed class WindowsApprovalPolicyEvaluatorTests
{
    [TestMethod]
    public void RememberedCommandsAutoAllow()
    {
        var policy = WindowsPolicyPreferences.Default with
        {
            RememberedAllowedCommands = ["pnpm test"],
        };

        Assert.IsTrue(WindowsApprovalPolicyEvaluator.ShouldAutoAllow(policy, "pnpm test"));
    }

    [TestMethod]
    public void RiskyCommandsAutoDenyWhenPolicyRequiresIt()
    {
        var policy = WindowsPolicyPreferences.Default with
        {
            ApprovalPolicy = WindowsApprovalPolicyPreference.DenyRiskyCommands,
        };

        Assert.IsTrue(WindowsApprovalPolicyEvaluator.ShouldAutoDeny(policy, "rm -rf dist"));
        Assert.IsFalse(WindowsApprovalPolicyEvaluator.ShouldAutoAllow(policy, "rm -rf dist"));
    }
}
