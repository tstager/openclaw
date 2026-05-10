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

public sealed record GatewayCliResolutionDiagnostics(
    string CommandName,
    IReadOnlyList<string> CandidateNames,
    IReadOnlyList<string> SearchDirectories,
    string? NodeExecutable,
    string? NpmExecutable,
    string? NpmPrefix,
    string? PowerShellCommandPath,
    string? ExpectedNpmCmdShim,
    bool ExpectedNpmCmdShimExists,
    string? ExpectedNpmPowerShellShim,
    bool ExpectedNpmPowerShellShimExists,
    string? ExpectedPackageEntry,
    bool ExpectedPackageEntryExists)
{
    public string FormatMissingCliMessage()
    {
        var builder = new StringBuilder();
        builder.AppendLine("OpenClaw CLI was not found.");
        builder.AppendLine();
        builder.AppendLine("The Windows app looked for:");
        foreach (var candidate in this.CandidateNames)
        {
            builder.AppendLine($"- {candidate}");
        }
        builder.AppendLine();
        builder.AppendLine("Searched locations:");
        foreach (var directory in this.SearchDirectories)
        {
            builder.AppendLine($"- {directory}");
        }
        builder.AppendLine();
        builder.AppendLine("Detected:");
        builder.AppendLine($"- node: {this.NodeExecutable ?? "not found"}");
        builder.AppendLine($"- npm: {this.NpmExecutable ?? "not found"}");
        builder.AppendLine($"- npm prefix: {this.NpmPrefix ?? "not found"}");
        builder.AppendLine($"- PowerShell command path: {this.PowerShellCommandPath ?? "not found"}");
        builder.AppendLine($"- expected cmd shim: {this.ExpectedNpmCmdShim ?? "not available"}");
        builder.AppendLine($"- expected cmd shim exists: {this.ExpectedNpmCmdShimExists}");
        builder.AppendLine($"- expected PowerShell shim: {this.ExpectedNpmPowerShellShim ?? "not available"}");
        builder.AppendLine($"- expected PowerShell shim exists: {this.ExpectedNpmPowerShellShimExists}");
        builder.AppendLine($"- expected package entry: {this.ExpectedPackageEntry ?? "not available"}");
        builder.AppendLine($"- expected package entry exists: {this.ExpectedPackageEntryExists}");
        builder.AppendLine();
        builder.AppendLine("Fix:");
        builder.AppendLine("Run `npm install -g openclaw`, then restart OpenClaw.");
        return builder.ToString().TrimEnd();
    }
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
        return ResolveExecutablePath(commandName, pathVariable, pathExtVariable, includeNpmPrefix: true);
    }

    private static string? ResolveExecutablePath(
        string commandName,
        string? pathVariable,
        string? pathExtVariable,
        bool includeNpmPrefix)
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
        foreach (var directory in GetExecutableSearchDirectories(pathVariable, includeNpmPrefix))
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

        if (includeNpmPrefix &&
            string.Equals(commandName, "openclaw", StringComparison.OrdinalIgnoreCase) &&
            QueryPowerShellCommandPath(commandName, pathVariable) is { } powerShellCommandPath)
        {
            return powerShellCommandPath;
        }

        return null;
    }

    public static GatewayCliResolutionDiagnostics CreateResolutionDiagnostics(string commandName)
    {
        var candidateNames = GetExecutableCandidateNames(commandName, null).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var searchDirectories = GetExecutableSearchDirectories(pathVariable: null).Take(48).ToArray();
        var nodeExecutable = ResolveExecutablePath("node", null, null, includeNpmPrefix: false);
        var npmExecutable = ResolveExecutablePath("npm", null, null, includeNpmPrefix: false);
        var npmPrefix = ResolveNpmPrefix(pathVariable: null);
        var powerShellCommandPath = QueryPowerShellCommandPath(commandName, pathVariable: null);
        var expectedCmdShim = string.IsNullOrWhiteSpace(npmPrefix)
            ? null
            : Path.Combine(npmPrefix, "openclaw.cmd");
        var expectedPowerShellShim = string.IsNullOrWhiteSpace(npmPrefix)
            ? null
            : Path.Combine(npmPrefix, "openclaw.ps1");
        var expectedPackageEntry = string.IsNullOrWhiteSpace(npmPrefix)
            ? null
            : Path.Combine(npmPrefix, "node_modules", "openclaw", "openclaw.mjs");
        return new GatewayCliResolutionDiagnostics(
            commandName,
            candidateNames,
            searchDirectories,
            nodeExecutable,
            npmExecutable,
            npmPrefix,
            powerShellCommandPath,
            expectedCmdShim,
            expectedCmdShim is not null && File.Exists(expectedCmdShim),
            expectedPowerShellShim,
            expectedPowerShellShim is not null && File.Exists(expectedPowerShellShim),
            expectedPackageEntry,
            expectedPackageEntry is not null && File.Exists(expectedPackageEntry));
    }

    public async Task<GatewayCliResult> RunAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        using var process = new Process { StartInfo = CreateProcessStartInfo(this, arguments) };
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
        yield return commandName + ".ps1";
    }

    private static IEnumerable<string> GetExecutableSearchDirectories(
        string? pathVariable,
        bool includeNpmPrefix = true)
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

        if (includeNpmPrefix)
        {
            foreach (var directory in GetNpmPrefixInstallDirectories(pathVariable))
            {
                if (seen.Add(directory))
                {
                    yield return directory;
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

    private static IEnumerable<string> GetNpmPrefixInstallDirectories(string? pathVariable)
    {
        var prefix = ResolveNpmPrefix(pathVariable);
        if (!string.IsNullOrWhiteSpace(prefix))
        {
            yield return Environment.ExpandEnvironmentVariables(prefix);
        }
    }

    private static string? ResolveNpmPrefix(string? pathVariable)
    {
        var prefix = Environment.GetEnvironmentVariable("NPM_CONFIG_PREFIX") ??
            Environment.GetEnvironmentVariable("npm_config_prefix");
        if (!string.IsNullOrWhiteSpace(prefix))
        {
            return Environment.ExpandEnvironmentVariables(prefix);
        }
        var npmPrefix = QueryNpmGlobalPrefix(pathVariable);
        if (!string.IsNullOrWhiteSpace(npmPrefix))
        {
            return Environment.ExpandEnvironmentVariables(npmPrefix);
        }
        return null;
    }

    private static string? QueryNpmGlobalPrefix(string? pathVariable)
    {
        var npm = ResolveExecutablePath("npm", pathVariable, null, includeNpmPrefix: false);
        if (string.IsNullOrWhiteSpace(npm))
        {
            return null;
        }

        using var process = new Process
        {
            StartInfo = CreateProcessStartInfo(CreateExecutableRunner(npm, "npm"), ["config", "get", "prefix"]),
        };
        try
        {
            process.Start();
            if (!process.WaitForExit(3000) || process.ExitCode != 0)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
                {
                }
                return null;
            }
        }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException or InvalidOperationException)
        {
            return null;
        }

        var output = process.StandardOutput.ReadToEnd().Trim();
        return string.IsNullOrWhiteSpace(output) ? null : output;
    }

    private static string? QueryPowerShellCommandPath(string commandName, string? pathVariable)
    {
        var escapedCommandName = commandName.Replace("'", "''", StringComparison.Ordinal);
        foreach (var powerShell in new[] { "pwsh", "powershell" })
        {
            var executable = ResolveExecutablePath(powerShell, pathVariable, null, includeNpmPrefix: false);
            if (string.IsNullOrWhiteSpace(executable))
            {
                continue;
            }

            using var process = new Process
            {
                StartInfo = CreateProcessStartInfo(
                    CreateExecutableRunner(executable, powerShell),
                    [
                        "-NoProfile",
                        "-ExecutionPolicy",
                        "Bypass",
                        "-Command",
                        $"$command = Get-Command -Name '{escapedCommandName}' -ErrorAction SilentlyContinue | Select-Object -First 1; if ($null -ne $command) {{ if ($command.Path) {{ $command.Path }} elseif ($command.Source) {{ $command.Source }} elseif ($command.Definition) {{ $command.Definition }} }}",
                    ]),
            };
            process.StartInfo.Environment["PATH"] = string.Join(
                Path.PathSeparator,
                GetExecutableSearchDirectories(pathVariable, includeNpmPrefix: false));
            try
            {
                process.Start();
                if (!process.WaitForExit(3000) || process.ExitCode != 0)
                {
                    try
                    {
                        process.Kill(entireProcessTree: true);
                    }
                    catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
                    {
                    }
                    continue;
                }
            }
            catch (Exception ex) when (ex is Win32Exception or FileNotFoundException or InvalidOperationException)
            {
                continue;
            }

            var output = process.StandardOutput.ReadToEnd().Trim();
            if (File.Exists(output))
            {
                return output;
            }
        }

        return null;
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

        if (extension.Equals(".ps1", StringComparison.OrdinalIgnoreCase) &&
            TryCreateNodeRunnerFromNpmPowerShellShim(executable, commandName) is { } nodeRunner)
        {
            return nodeRunner;
        }

        if (extension.Equals(".ps1", StringComparison.OrdinalIgnoreCase) &&
            TryCreatePowerShellShimRunner(executable, commandName) is { } powerShellRunner)
        {
            return powerShellRunner;
        }

        return new GatewayCliCommandRunner(executable, [], commandName);
    }

    private static GatewayCliCommandRunner? TryCreateNodeRunnerFromNpmPowerShellShim(
        string shimPath,
        string commandName)
    {
        if (!string.Equals(commandName, "openclaw", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var shimDirectory = Path.GetDirectoryName(shimPath);
        if (string.IsNullOrWhiteSpace(shimDirectory))
        {
            return null;
        }

        var packageEntry = Path.Combine(shimDirectory, "node_modules", "openclaw", "openclaw.mjs");
        if (!File.Exists(packageEntry))
        {
            return null;
        }

        var localNode = Path.Combine(shimDirectory, "node.exe");
        var nodeExecutable = File.Exists(localNode)
            ? localNode
            : ResolveExecutablePath("node", null, null, includeNpmPrefix: false) ?? "node";
        return new GatewayCliCommandRunner(nodeExecutable, [packageEntry], commandName);
    }

    private static GatewayCliCommandRunner? TryCreatePowerShellShimRunner(
        string shimPath,
        string commandName)
    {
        if (!File.Exists(shimPath))
        {
            return null;
        }

        var powerShell = ResolveExecutablePath("pwsh", null, null, includeNpmPrefix: false) ??
            ResolveExecutablePath("powershell", null, null, includeNpmPrefix: false);
        if (string.IsNullOrWhiteSpace(powerShell))
        {
            return null;
        }

        return new GatewayCliCommandRunner(
            powerShell,
            ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", shimPath],
            commandName);
    }

    private static ProcessStartInfo CreateProcessStartInfo(
        GatewayCliCommandRunner runner,
        IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = runner.Executable,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
        };

        foreach (var argument in runner.BaseArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    private static GatewayCliResult MissingCommandResult(string commandName)
    {
        var message = string.Equals(commandName, "node", StringComparison.OrdinalIgnoreCase) ||
            commandName.StartsWith("node ", StringComparison.OrdinalIgnoreCase)
            ? "Node runtime was not found on PATH. Install Node.js 22 or newer, then restart the app."
            : CreateResolutionDiagnostics(commandName).FormatMissingCliMessage();
        return new GatewayCliResult(1, "", message);
    }
}
