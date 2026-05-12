using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace OpenClaw.Windows.Native;

/// <summary>
/// Registers and dispatches the app's process-local Ctrl+Shift+Space push-to-talk hotkey.
/// </summary>
public sealed class WindowsGlobalHotkeyService : IDisposable
{
    private const int HotkeyId = 0x434c4157;
    private readonly HotkeyWindow window;
    private bool disposed;

    public WindowsGlobalHotkeyService(Action onPressed)
    {
        this.window = new HotkeyWindow(onPressed);
    }

    public bool IsRegistered { get; private set; }

    /// <summary>
    /// Registers the global hotkey with user32 and throws when another app already owns it.
    /// </summary>
    public void RegisterPushToTalkHotkey()
    {
        if (this.IsRegistered)
        {
            return;
        }

        if (!NativeMethods.RegisterHotKey(this.window.Handle, HotkeyId, HotkeyModifiers.Control | HotkeyModifiers.Shift, (uint)Keys.Space))
        {
            throw new InvalidOperationException("Windows rejected the Ctrl+Shift+Space hotkey registration.");
        }

        this.IsRegistered = true;
    }

    /// <summary>
    /// Releases the hotkey registration while keeping the hidden message window alive.
    /// </summary>
    public void Unregister()
    {
        if (!this.IsRegistered)
        {
            return;
        }

        _ = NativeMethods.UnregisterHotKey(this.window.Handle, HotkeyId);
        this.IsRegistered = false;
    }

    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.Unregister();
        this.window.Dispose();
        this.disposed = true;
    }

    /// <summary>
    /// Hidden Win32 message sink that receives WM_HOTKEY outside the WinUI input tree.
    /// </summary>
    private sealed class HotkeyWindow : NativeWindow, IDisposable
    {
        private const int WmHotkey = 0x0312;
        private readonly Action onPressed;

        public HotkeyWindow(Action onPressed)
        {
            this.onPressed = onPressed;
            this.CreateHandle(new CreateParams());
        }

        public void Dispose()
        {
            this.DestroyHandle();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmHotkey && m.WParam.ToInt32() == HotkeyId)
            {
                this.onPressed();
                return;
            }

            base.WndProc(ref m);
        }
    }
}

/// <summary>
/// user32 modifier bitmask values passed to RegisterHotKey.
/// </summary>
[Flags]
internal enum HotkeyModifiers : uint
{
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Win = 0x0008,
}

/// <summary>
/// P/Invoke declarations for the global hotkey APIs.
/// </summary>
internal static partial class NativeMethods
{
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool RegisterHotKey(nint hWnd, int id, HotkeyModifiers fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnregisterHotKey(nint hWnd, int id);
}
