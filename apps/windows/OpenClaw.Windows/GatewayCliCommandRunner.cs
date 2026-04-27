using System.Diagnostics;
using System.Text;

namespace OpenClaw.Windows;

public interface IGatewayCliCommandRunner
{
    string CommandName { get; }

    Task<GatewayCliResult> RunAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default);
}

public sealed record GatewayCliResult(
    int ExitCode,
    string StandardOutput,
    string StandardError)
{
    public bool Succeeded => this.ExitCode == 0;

    public string CombinedOutput => string.Join(
        Environment.NewLine,
        new[] { this.StandardOutput.Trim(), this.StandardError.Trim() }.Where(static line => line.Length > 0));
}

public sealed class GatewayCliCommandRunner(string commandName) : IGatewayCliCommandRunner
{
    public string CommandName { get; } = commandName;

    public async Task<GatewayCliResult> RunAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = this.CommandName,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, args) =>
        {
            if (args.Data is not null)
            {
                stdout.AppendLine(args.Data);
            }
        };
        process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data is not null)
            {
                stderr.AppendLine(args.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync(cancellationToken);

        return new GatewayCliResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }
}
