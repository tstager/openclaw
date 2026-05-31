namespace OpenClaw.Windows;

/// <summary>
/// A rectangle in physical screen pixels, mirroring the fields the flyout needs without taking a WinUI dependency.
/// </summary>
public readonly record struct TrayPixelRect(int X, int Y, int Width, int Height);

/// <summary>
/// Computes where the tray flyout should sit so it hugs the tray icon and stays fully inside the work area.
/// Pure geometry so it can be unit tested without the WinUI runtime.
/// </summary>
public static class TrayFlyoutPositioner
{
    /// <summary>
    /// Gap in physical pixels left between the flyout and the work-area edges and the tray anchor.
    /// </summary>
    public const int EdgeMargin = 8;

    /// <summary>
    /// Places a flyout of <paramref name="flyoutWidth"/> x <paramref name="flyoutHeight"/> near
    /// <paramref name="anchor"/> (the tray icon or cursor position), clamped inside <paramref name="workArea"/>.
    /// The flyout prefers to sit above-and-left of the anchor like a tray menu.
    /// </summary>
    public static TrayPixelRect Place(
        TrayPixelRect workArea,
        int anchor,
        int anchorY,
        int flyoutWidth,
        int flyoutHeight)
    {
        // Never let the flyout exceed the work area; an oversized flyout is capped here and the view scrolls
        // its overflow rather than running off-screen or painting a blank region past the edge.
        var width = Math.Min(flyoutWidth, Math.Max(0, workArea.Width - (2 * EdgeMargin)));
        var height = Math.Min(flyoutHeight, Math.Max(0, workArea.Height - (2 * EdgeMargin)));

        var x = anchor - width + EdgeMargin;
        var y = anchorY - height - EdgeMargin;

        var maxX = workArea.X + workArea.Width - width - EdgeMargin;
        var minX = workArea.X + EdgeMargin;
        x = Clamp(x, minX, maxX);

        var maxY = workArea.Y + workArea.Height - height - EdgeMargin;
        var minY = workArea.Y + EdgeMargin;
        y = Clamp(y, minY, maxY);

        return new TrayPixelRect(x, y, width, height);
    }

    private static int Clamp(int value, int min, int max)
    {
        // When the flyout is larger than the available span, prefer pinning to the near (min) edge.
        if (max < min)
        {
            return min;
        }

        return Math.Clamp(value, min, max);
    }
}
