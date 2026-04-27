namespace OpenClaw.Windows;

public enum OnboardingCheckState
{
    Passed,
    Warning,
    Failed,
}

public sealed record OnboardingCheckResult(
    string Key,
    string Label,
    OnboardingCheckState State,
    string Detail);

public sealed class OnboardingCheckService(IGatewayCliCommandRunner commandRunner)
{
    private readonly IGatewayCliCommandRunner commandRunner = commandRunner;

    public async Task<IReadOnlyList<OnboardingCheckResult>> RunAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<OnboardingCheckResult>
        {
            await CheckCommandAsync("openclaw", this.commandRunner.CommandName, ["--version"], cancellationToken),
            await CheckCommandAsync("node", "node", ["--version"], cancellationToken),
        };

        var status = await this.commandRunner.RunAsync(["gateway", "status", "--json"], cancellationToken);
        if (!status.Succeeded)
        {
            results.Add(new OnboardingCheckResult(
                "gateway",
                "Gateway status",
                OnboardingCheckState.Warning,
                "Gateway status is not available yet. Install or start the gateway from the app."));
            results.Add(new OnboardingCheckResult(
                "pairing",
                "Pairing readiness",
                OnboardingCheckState.Warning,
                "Pairing can be checked after the gateway responds."));
            return results;
        }

        var snapshot = GatewayStatusSnapshot.FromJson(status.StandardOutput);
        results.Add(new OnboardingCheckResult(
            "gateway",
            "Gateway status",
            snapshot.Reachable ? OnboardingCheckState.Passed : OnboardingCheckState.Warning,
            snapshot.Reachable ? "Gateway is reachable." : "Gateway service exists but the probe is not reachable."));
        results.Add(new OnboardingCheckResult(
            "pairing",
            "Pairing readiness",
            string.Equals(snapshot.Capability, "pairing_pending", StringComparison.OrdinalIgnoreCase)
                ? OnboardingCheckState.Warning
                : OnboardingCheckState.Passed,
            string.Equals(snapshot.Capability, "pairing_pending", StringComparison.OrdinalIgnoreCase)
                ? "Gateway is reachable but still waiting for pairing approval."
                : $"Gateway capability: {snapshot.Capability}."));

        return results;
    }

    private static async Task<OnboardingCheckResult> CheckCommandAsync(
        string key,
        string command,
        IReadOnlyList<string> args,
        CancellationToken cancellationToken)
    {
        try
        {
            var runner = new GatewayCliCommandRunner(command);
            var result = await runner.RunAsync(args, cancellationToken);
            return new OnboardingCheckResult(
                key,
                key == "node" ? "Node runtime" : "OpenClaw CLI",
                result.Succeeded ? OnboardingCheckState.Passed : OnboardingCheckState.Failed,
                result.Succeeded ? result.CombinedOutput : $"Command failed: {result.CombinedOutput}");
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            return new OnboardingCheckResult(
                key,
                key == "node" ? "Node runtime" : "OpenClaw CLI",
                OnboardingCheckState.Failed,
                $"{command} was not found on PATH.");
        }
    }
}
