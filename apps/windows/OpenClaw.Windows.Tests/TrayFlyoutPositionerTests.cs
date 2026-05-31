using OpenClaw.Windows;

namespace OpenClaw.Windows.Tests;

[TestClass]
public sealed class TrayFlyoutPositionerTests
{
    // A 1920x1040 work area on a primary display whose taskbar sits at the bottom-right.
    private static readonly TrayPixelRect WorkArea = new(0, 0, 1920, 1040);

    [TestMethod]
    public void PlacesFlyoutAboveAndLeftOfTrayAnchor()
    {
        var placement = TrayFlyoutPositioner.Place(WorkArea, anchor: 1850, anchorY: 1030, flyoutWidth: 300, flyoutHeight: 260);

        Assert.AreEqual(1850 - 300 + TrayFlyoutPositioner.EdgeMargin, placement.X);
        Assert.AreEqual(1030 - 260 - TrayFlyoutPositioner.EdgeMargin, placement.Y);
        Assert.AreEqual(300, placement.Width);
        Assert.AreEqual(260, placement.Height);
    }

    [TestMethod]
    public void ClampsToRightEdgeWhenAnchorIsPastTheWorkArea()
    {
        var placement = TrayFlyoutPositioner.Place(WorkArea, anchor: 5000, anchorY: 1030, flyoutWidth: 300, flyoutHeight: 260);

        Assert.AreEqual(WorkArea.Width - 300 - TrayFlyoutPositioner.EdgeMargin, placement.X);
    }

    [TestMethod]
    public void ClampsToLeftEdgeWhenAnchorIsNearOrigin()
    {
        var placement = TrayFlyoutPositioner.Place(WorkArea, anchor: 5, anchorY: 1030, flyoutWidth: 300, flyoutHeight: 260);

        Assert.AreEqual(WorkArea.X + TrayFlyoutPositioner.EdgeMargin, placement.X);
    }

    [TestMethod]
    public void ClampsToBottomWhenTrayIsAtTheTop()
    {
        var placement = TrayFlyoutPositioner.Place(WorkArea, anchor: 960, anchorY: 5, flyoutWidth: 300, flyoutHeight: 260);

        Assert.AreEqual(WorkArea.Y + TrayFlyoutPositioner.EdgeMargin, placement.Y);
    }

    [TestMethod]
    public void HonorsWorkAreaOriginOffsetFromSecondaryDisplay()
    {
        var secondary = new TrayPixelRect(1920, 0, 1280, 1024);

        var placement = TrayFlyoutPositioner.Place(secondary, anchor: 3180, anchorY: 1010, flyoutWidth: 300, flyoutHeight: 260);

        Assert.IsGreaterThanOrEqualTo(secondary.X + TrayFlyoutPositioner.EdgeMargin, placement.X);
        Assert.IsLessThanOrEqualTo(secondary.X + secondary.Width - TrayFlyoutPositioner.EdgeMargin, placement.X + placement.Width);
        Assert.IsGreaterThanOrEqualTo(secondary.Y + TrayFlyoutPositioner.EdgeMargin, placement.Y);
    }

    [TestMethod]
    public void OversizedFlyoutPinsToNearEdgeInsteadOfOverflowing()
    {
        var tiny = new TrayPixelRect(0, 0, 200, 200);

        var placement = TrayFlyoutPositioner.Place(tiny, anchor: 150, anchorY: 150, flyoutWidth: 300, flyoutHeight: 260);

        Assert.AreEqual(tiny.X + TrayFlyoutPositioner.EdgeMargin, placement.X);
        Assert.AreEqual(tiny.Y + TrayFlyoutPositioner.EdgeMargin, placement.Y);
    }

    [TestMethod]
    public void ClampsFlyoutHeightToWorkAreaWhenContentIsTallerThanScreen()
    {
        var placement = TrayFlyoutPositioner.Place(WorkArea, anchor: 1850, anchorY: 1030, flyoutWidth: 300, flyoutHeight: 5000);

        Assert.AreEqual(WorkArea.Height - (2 * TrayFlyoutPositioner.EdgeMargin), placement.Height);
        Assert.AreEqual(WorkArea.Y + TrayFlyoutPositioner.EdgeMargin, placement.Y);
    }

    [TestMethod]
    public void ClampsFlyoutWidthToWorkAreaWhenWiderThanScreen()
    {
        var narrow = new TrayPixelRect(0, 0, 220, 1040);

        var placement = TrayFlyoutPositioner.Place(narrow, anchor: 200, anchorY: 1030, flyoutWidth: 300, flyoutHeight: 260);

        Assert.AreEqual(narrow.Width - (2 * TrayFlyoutPositioner.EdgeMargin), placement.Width);
        Assert.AreEqual(narrow.X + TrayFlyoutPositioner.EdgeMargin, placement.X);
    }

    [TestMethod]
    public void OversizedFlyoutIsCappedToWorkAreaSize()
    {
        var tiny = new TrayPixelRect(0, 0, 200, 200);

        var placement = TrayFlyoutPositioner.Place(tiny, anchor: 150, anchorY: 150, flyoutWidth: 300, flyoutHeight: 260);

        Assert.AreEqual(tiny.Width - (2 * TrayFlyoutPositioner.EdgeMargin), placement.Width);
        Assert.AreEqual(tiny.Height - (2 * TrayFlyoutPositioner.EdgeMargin), placement.Height);
    }
}
