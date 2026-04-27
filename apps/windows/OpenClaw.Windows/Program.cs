using System;
using WinRT;
using XamlApplication = Microsoft.UI.Xaml.Application;

namespace OpenClaw.Windows;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ComWrappersSupport.InitializeComWrappers();
        XamlApplication.Start(_ => new App());
    }
}
