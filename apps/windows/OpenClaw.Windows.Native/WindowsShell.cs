using System.Diagnostics;

namespace OpenClaw.Windows.Native;

/// <summary>
/// Thin wrapper around Explorer shell actions used by log and capture buttons.
/// </summary>
public static class WindowsShell
{
    /// <summary>
    /// Opens Explorer at the folder or selects the file when the target exists.
    /// </summary>
    public static void OpenFileInExplorer(string path)
    {
        var target = File.Exists(path) ? $"/select,\"{path}\"" : $"\"{path}\"";
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = target,
            UseShellExecute = true,
        });
    }
}
