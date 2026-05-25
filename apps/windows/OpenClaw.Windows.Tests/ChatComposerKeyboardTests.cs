using OpenClaw.Windows;
using Windows.System;

namespace OpenClaw.Windows.Tests;

[TestClass]
public sealed class ChatComposerKeyboardTests
{
    [TestMethod]
    public void IsSendShortcutRequiresControlEnter()
    {
        Assert.IsTrue(ChatComposerKeyboard.IsSendShortcut(VirtualKey.Enter, controlDown: true));
        Assert.IsFalse(ChatComposerKeyboard.IsSendShortcut(VirtualKey.Enter, controlDown: false));
        Assert.IsFalse(ChatComposerKeyboard.IsSendShortcut(VirtualKey.S, controlDown: true));
    }
}
