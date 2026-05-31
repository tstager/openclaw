using OpenClaw.Windows.Native;

namespace OpenClaw.Windows;

/// <summary>
/// The capabilities, commands, and permission claims the Windows node advertises to the gateway on connect.
/// </summary>
/// <remarks>
/// Built purely from the host capability probe and the current app preferences so the advertised surface is
/// honest: a capability the host cannot provide, or a feature the user disabled, is left out. Command and
/// capability strings match the gateway node-command policy for the <c>windows</c> platform.
/// </remarks>
public sealed record WindowsNodeSurface(
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<string> Commands,
    IReadOnlyDictionary<string, bool> Permissions)
{
    public static WindowsNodeSurface Build(WindowsHostCapabilities host, AppPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(preferences);

        var capabilities = new List<string>();
        var commands = new List<string>();
        var permissions = new Dictionary<string, bool>(StringComparer.Ordinal);

        if (preferences.CanvasNodeEnabled)
        {
            capabilities.Add("canvas");
            commands.AddRange(WindowsCanvasCommands.All);
            permissions["canvas.a2ui"] = true;
        }

        if (host.SupportsScreenCapture)
        {
            capabilities.Add("screen");
            commands.Add("screen.snapshot");
            if (host.SupportsScreenRecording)
            {
                commands.Add("screen.record");
            }
            permissions["screen.record"] = host.SupportsScreenRecording;
        }

        if (host.SupportsCameraCapture)
        {
            capabilities.Add("camera");
            commands.Add("camera.list");
            commands.Add("camera.snap");
            permissions["camera.capture"] = true;
        }

        if (host.SupportsMicrophoneCapture)
        {
            permissions["microphone"] = preferences.VoiceControlsEnabled;
        }

        if (host.SupportsToastNotifications)
        {
            permissions["notifications"] = true;
        }

        return new WindowsNodeSurface(capabilities, commands, permissions);
    }
}
