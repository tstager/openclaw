using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Microsoft.Win32;

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
        : this(ResolveExecutablePath(commandName) ?? commandName, [], commandName)
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
            CreateGlobalOpenClawRunner();
    }

    public static GatewayCliCommandRunner CreateGlobalOpenClawRunner()
    {
        var executable = ResolveExecutablePath("openclaw") ?? "openclaw";
        return CreateExecutableRunner(executable, "openclaw");
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

    public static string? ResolveExecutablePath(
        string commandName,
        string? pathVariable = null,
        string? pathExtVariable = null)
    {
        if (string.IsNullOrWhiteSpace(commandName))
        {
            return null;
        }

        if (Path.IsPathFullyQualified(commandName) || commandName.Contains(Path.DirectorySeparatorChar) ||
            commandName.Contains(Path.AltDirectorySeparatorChar))
        {
            return File.Exists(commandName) ? commandName : null;
        }

        var candidateNames = GetExecutableCandidateNames(commandName, pathExtVariable).ToArray();
        foreach (var directory in GetExecutableSearchDirectories(pathVariable))
        {
            foreach (var candidateName in candidateNames)
            {
                var candidate = Path.Combine(directory, candidateName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
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

    private static IEnumerable<string> GetExecutableCandidateNames(string commandName, string? pathExtVariable)
    {
        if (Path.HasExtension(commandName))
        {
            yield return commandName;
            yield break;
        }

        yield return commandName;
        var pathExt = pathExtVariable ?? Environment.GetEnvironmentVariable("PATHEXT");
        foreach (var extension in (pathExt ?? ".COM;.EXE;.BAT;.CMD").Split(
            Path.PathSeparator,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return commandName + extension.ToLowerInvariant();
        }
    }

    private static IEnumerable<string> GetExecutableSearchDirectories(string? pathVariable)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in GetPathVariables(pathVariable))
        {
            foreach (var directory in path.Split(
                Path.PathSeparator,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var expanded = Environment.ExpandEnvironmentVariables(directory);
                if (seen.Add(expanded))
                {
                    yield return expanded;
                }
            }
        }

        foreach (var directory in GetDefaultNodeInstallDirectories())
        {
            if (seen.Add(directory))
            {
                yield return directory;
            }
        }
    }

    private static IEnumerable<string> GetPathVariables(string? pathVariable)
    {
        if (!string.IsNullOrWhiteSpace(pathVariable))
        {
            yield return pathVariable;
        }

        var processPath = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(processPath))
        {
            yield return processPath;
        }

        var userPath = Registry.GetValue(
            @"HKEY_CURRENT_USER\Environment",
            "Path",
            null) as string;
        if (!string.IsNullOrWhiteSpace(userPath))
        {
            yield return userPath;
        }

        var machinePath = Registry.GetValue(
            @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Environment",
            "Path",
            null) as string;
        if (!string.IsNullOrWhiteSpace(machinePath))
        {
            yield return machinePath;
        }
    }

    private static IEnumerable<string> GetDefaultNodeInstallDirectories()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrWhiteSpace(appData))
        {
            yield return Path.Combine(appData, "npm");
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            yield return Path.Combine(userProfile, "AppData", "Roaming", "npm");
        }

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrWhiteSpace(programFiles))
        {
            yield return Path.Combine(programFiles, "nodejs");
        }

        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrWhiteSpace(programFilesX86))
        {
            yield return Path.Combine(programFilesX86, "nodejs");
        }
    }

    private static GatewayCliCommandRunner CreateExecutableRunner(string executable, string commandName)
    {
        var extension = Path.GetExtension(executable);
        if (extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".bat", StringComparison.OrdinalIgnoreCase))
        {
            var shell = Environment.GetEnvironmentVariable("ComSpec");
            if (string.IsNullOrWhiteSpace(shell))
            {
                shell = Path.Combine(Environment.SystemDirectory, "cmd.exe");
            }
            return new GatewayCliCommandRunner(shell, ["/d", "/c", executable], commandName);
        }

        return new GatewayCliCommandRunner(executable, [], commandName);
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
