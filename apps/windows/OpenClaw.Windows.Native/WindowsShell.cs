using System.Diagnostics;

namespace OpenClaw.Windows.Native;

public static class WindowsShell
{
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
