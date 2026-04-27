using OpenClaw.Windows;

namespace OpenClaw.Windows.Tests;

internal sealed class FakeGatewayCliCommandRunner(params GatewayCliResult[] results) : IGatewayCliCommandRunner
{
    private readonly Queue<GatewayCliResult> results = new(results);

    public string CommandName => "openclaw";

    public List<IReadOnlyList<string>> Calls { get; } = [];

    public Task<GatewayCliResult> RunAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        this.Calls.Add(arguments.ToArray());
        return Task.FromResult(this.results.Count > 0
            ? this.results.Dequeue()
            : new GatewayCliResult(0, """{"ok":true}""", ""));
    }
}
