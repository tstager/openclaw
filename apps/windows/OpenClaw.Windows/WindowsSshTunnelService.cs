using System.Diagnostics;
using System.Text;

namespace OpenClaw.Windows;

/// <summary>
/// Snapshot of the optional SSH port-forward process state.
/// </summary>
public sealed record WindowsSshTunnelStatus(
    bool Running,
    string Summary,
    string? LastError);

/// <summary>
/// Starts and stops a simple local SSH port forward used by topology-focused workflows.
/// </summary>
public sealed class WindowsSshTunnelService : IDisposable
{
    private readonly object gate = new();
    private readonly StringBuilder processOutput = new();
    private Process? process;

    public WindowsSshTunnelStatus Status { get; private set; } = new(
        Running: false,
        Summary: "Tunnel not running.",
        LastError: null);

    public event Action<WindowsSshTunnelStatus>? StatusChanged;

    public async Task ApplyPreferencesAsync(
        WindowsTopologyPreferences preferences,
        CancellationToken cancellationToken = default)
    {
        if (!preferences.AutoStartTunnel || !IsConfigured(preferences))
        {
            this.Stop();
            return;
        }

        await this.StartAsync(preferences, cancellationToken);
    }

    public async Task StartAsync(
        WindowsTopologyPreferences preferences,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured(preferences))
        {
            this.UpdateStatus(new WindowsSshTunnelStatus(
                Running: false,
                Summary: "Tunnel is not configured.",
                LastError: "Configure SSH host, remote host, and valid local/remote ports."));
            return;
        }

        lock (this.gate)
        {
            if (this.process is { HasExited: false })
            {
                return;
            }

            this.processOutput.Clear();
        }

        var process = new Process
        {
            StartInfo = new ProcessStartInfo("ssh")
            {
                Arguments =
                    $"-N -L {preferences.LocalPort}:{preferences.RemoteHost}:{preferences.RemotePort} {preferences.SshHost} " +
                    "-o BatchMode=yes -o ConnectTimeout=10 -o ExitOnForwardFailure=yes",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
            EnableRaisingEvents = true,
        };
        process.OutputDataReceived += this.OnProcessOutput;
        process.ErrorDataReceived += this.OnProcessOutput;
        process.Exited += (_, _) => this.OnProcessExited();

        if (!process.Start())
        {
            this.UpdateStatus(new WindowsSshTunnelStatus(false, "Tunnel failed to start.", "ssh did not start."));
            process.Dispose();
            return;
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        lock (this.gate)
        {
            this.process = process;
        }

        await Task.Delay(250, cancellationToken);
        if (process.HasExited)
        {
            this.OnProcessExited();
            return;
        }

        this.UpdateStatus(new WindowsSshTunnelStatus(
            Running: true,
            Summary: $"Forwarding localhost:{preferences.LocalPort} to {preferences.RemoteHost}:{preferences.RemotePort} via {preferences.SshHost}.",
            LastError: null));
    }

    public void Stop()
    {
        Process? processToStop;
        lock (this.gate)
        {
            processToStop = this.process;
            this.process = null;
        }

        if (processToStop is null)
        {
            this.UpdateStatus(new WindowsSshTunnelStatus(false, "Tunnel not running.", null));
            return;
        }

        if (!processToStop.HasExited)
        {
            processToStop.Kill(entireProcessTree: true);
            processToStop.WaitForExit();
        }

        processToStop.Dispose();
        this.UpdateStatus(new WindowsSshTunnelStatus(false, "Tunnel stopped.", null));
    }

    public void Dispose()
    {
        this.Stop();
    }

    private static bool IsConfigured(WindowsTopologyPreferences preferences)
    {
        return !string.IsNullOrWhiteSpace(preferences.SshHost) &&
            !string.IsNullOrWhiteSpace(preferences.RemoteHost) &&
            preferences.LocalPort > 0 &&
            preferences.RemotePort > 0;
    }

    private void OnProcessOutput(object sender, DataReceivedEventArgs args)
    {
        if (string.IsNullOrWhiteSpace(args.Data))
        {
            return;
        }

        lock (this.gate)
        {
            if (this.processOutput.Length > 0)
            {
                this.processOutput.AppendLine();
            }

            this.processOutput.Append(args.Data.Trim());
        }
    }

    private void OnProcessExited()
    {
        string? output;
        lock (this.gate)
        {
            output = this.processOutput.Length == 0 ? null : this.processOutput.ToString();
            this.process?.Dispose();
            this.process = null;
        }

        this.UpdateStatus(new WindowsSshTunnelStatus(
            Running: false,
            Summary: "Tunnel stopped.",
            LastError: string.IsNullOrWhiteSpace(output) ? null : output));
    }

    private void UpdateStatus(WindowsSshTunnelStatus status)
    {
        this.Status = status;
        this.StatusChanged?.Invoke(status);
    }
}
