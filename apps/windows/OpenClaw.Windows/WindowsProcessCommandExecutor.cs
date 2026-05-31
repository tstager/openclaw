using System.ComponentModel;
using System.Diagnostics;

namespace OpenClaw.Windows;

/// <summary>
/// Runs Windows commands through <see cref="Process"/> with bounded output capture and timeout enforcement, and
/// resolves executables against PATH/PATHEXT for <c>system.which</c>.
/// </summary>
public sealed class WindowsProcessCommandExecutor : IWindowsCommandExecutor
{
    public async Task<WindowsCommandRunResult> RunAsync(
        IReadOnlyList<string> command,
        string? cwd,
        IReadOnlyDictionary<string, string>? env,
        int? timeoutMs,
        CancellationToken cancellationToken)
    {
        if (command.Count == 0)
        {
            return new WindowsCommandRunResult(null, false, false, string.Empty, string.Empty, "command required");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = command[0],
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        if (!string.IsNullOrWhiteSpace(cwd))
        {
            startInfo.WorkingDirectory = cwd;
        }
        for (var index = 1; index < command.Count; index++)
        {
            startInfo.ArgumentList.Add(command[index]);
        }
        if (env is not null)
        {
            foreach (var entry in env)
            {
                startInfo.Environment[entry.Key] = entry.Value;
            }
        }

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                return new WindowsCommandRunResult(null, false, false, string.Empty, string.Empty, "Failed to start process.");
            }
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            return new WindowsCommandRunResult(null, false, false, string.Empty, string.Empty, ex.Message);
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeoutMs is > 0)
        {
            timeoutCts.CancelAfter(timeoutMs.Value);
        }

        var timedOut = false;
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            timedOut = true;
        }

        var stdout = await SafeReadAsync(stdoutTask);
        var stderr = await SafeReadAsync(stderrTask);
        int? exitCode = null;
        if (!timedOut)
        {
            try
            {
                exitCode = process.ExitCode;
            }
            catch (InvalidOperationException)
            {
            }
        }

        var success = !timedOut && exitCode == 0;
        return new WindowsCommandRunResult(
            exitCode,
            timedOut,
            success,
            stdout,
            stderr,
            timedOut ? "Command timed out." : null);
    }

    public Task<string?> WhichAsync(string command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return Task.FromResult<string?>(null);
        }

        if (Path.IsPathRooted(command) && File.Exists(command))
        {
            return Task.FromResult<string?>(command);
        }

        var pathExtensions = (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT;.COM")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var directories = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var hasExtension = !string.IsNullOrEmpty(Path.GetExtension(command));

        foreach (var directory in directories)
        {
            string basePath;
            try
            {
                basePath = Path.Combine(directory, command);
            }
            catch (ArgumentException)
            {
                continue;
            }

            if (hasExtension)
            {
                if (File.Exists(basePath))
                {
                    return Task.FromResult<string?>(basePath);
                }
                continue;
            }

            foreach (var extension in pathExtensions)
            {
                var candidate = basePath + extension;
                if (File.Exists(candidate))
                {
                    return Task.FromResult<string?>(candidate);
                }
            }
        }

        return Task.FromResult<string?>(null);
    }

    private static async Task<string> SafeReadAsync(Task<string> readTask)
    {
        try
        {
            return await readTask;
        }
        catch (OperationCanceledException)
        {
            return string.Empty;
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException)
        {
        }
    }
}
