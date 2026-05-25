using Windows.System;

namespace OpenClaw.Windows;

public static class ChatComposerKeyboard
{
    public static bool IsSendShortcut(VirtualKey key, bool controlDown)
    {
        return key == VirtualKey.Enter && controlDown;
    }
}
