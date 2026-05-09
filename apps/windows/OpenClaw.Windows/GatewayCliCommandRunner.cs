using System.ComponentModel;
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

public sealed class GatewayCliCommandRunner : IGatewayCliCommandRunner
{
    private readonly IReadOnlyList<string> baseArguments;

    public GatewayCliCommandRunner(string commandName)
        : this(commandName, [], commandName)
    {
    }

    private GatewayCliCommandRunner(
        string executable,
        IReadOnlyList<string> baseArguments,
        string commandName)
    {
        this.Executable = executable;
        this.baseArguments = baseArguments;
        this.CommandName = commandName;
    }

    public string CommandName { get; }

    public string Executable { get; }

    public IReadOnlyList<string> BaseArguments => this.baseArguments;

    public static GatewayCliCommandRunner CreateDefault()
    {
        return TryCreateFromSourceCheckout(Directory.GetCurrentDirectory(), AppContext.BaseDirectory) ??
            new GatewayCliCommandRunner("openclaw");
    }

    public static GatewayCliCommandRunner? TryCreateFromSourceCheckout(params string[] startDirectories)
    {
        foreach (var startDirectory in startDirectories)
        {
            var root = FindRepoRoot(startDirectory);
            if (root is null)
            {
                continue;
            }

            var cliPath = Path.Combine(root, "openclaw.mjs");
            return new GatewayCliCommandRunner(
                "node",
                [cliPath],
                $"node {cliPath}");
        }

        return null;
    }

    public async Task<GatewayCliResult> RunAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = this.Executable,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
        };

        foreach (var argument in this.baseArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

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

        try
        {
            process.Start();
        }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException)
        {
            return MissingCommandResult(this.CommandName);
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync(cancellationToken);

        return new GatewayCliResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }

    private static string? FindRepoRoot(string startDirectory)
    {
        if (string.IsNullOrWhiteSpace(startDirectory))
        {
            return null;
        }

        var current = new DirectoryInfo(Path.GetFullPath(startDirectory));
        while (current is not null)
        {
            if (
                File.Exists(Path.Combine(current.FullName, "openclaw.mjs")) &&
                File.Exists(Path.Combine(current.FullName, "package.json")) &&
                File.Exists(Path.Combine(current.FullName, "dist", "entry.mjs")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }

    private static GatewayCliResult MissingCommandResult(string commandName)
    {
        var message = string.Equals(commandName, "node", StringComparison.OrdinalIgnoreCase) ||
            commandName.StartsWith("node ", StringComparison.OrdinalIgnoreCase)
            ? "Node runtime was not found on PATH. Install Node.js 22 or newer, then restart the app."
            : "OpenClaw CLI was not found. Install OpenClaw for Windows or add openclaw to PATH, then restart the app.";
        return new GatewayCliResult(1, "", message);
    }
}
