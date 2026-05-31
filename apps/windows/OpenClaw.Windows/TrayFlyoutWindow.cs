using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using OpenClaw.Windows.Native;
using Windows.Graphics;
using Windows.UI;
using XamlButton = Microsoft.UI.Xaml.Controls.Button;
using XamlHorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment;
using XamlOrientation = Microsoft.UI.Xaml.Controls.Orientation;
using XamlVerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment;

namespace OpenClaw.Windows;

/// <summary>
/// Compact, themed, light-dismiss WinUI window that replaces the WinForms tray context menu.
/// It renders a <see cref="TrayFlyoutModel"/> with status-dot rows, separators, icon action rows, and
/// permission toggles, anchors itself near the tray icon, and closes when it loses focus. Content scrolls
/// when it is taller than the work area, the window is sized to its content at the display's DPI, and rows
/// highlight on hover from the resolved theme palette. New rows are added via the model, not this view.
/// </summary>
public sealed class TrayFlyoutWindow : Window
{
    // Content width and chrome are expressed in DIPs; the window is sized in physical pixels by multiplying
    // through the display's rasterization scale so the flyout is not clipped on high-DPI screens.
    private const double FlyoutContentWidth = 300;
    private const double ChromeThickness = 14;

    // Segoe Fluent Icons "Accept" check, shown accent-tinted when a toggle is on and dimmed when off.
    private const string ToggleIndicatorGlyph = "";

    private readonly Action<TrayFlyoutAction> onAction;
    private readonly Action<TrayFlyoutAction> onToggle;
    private readonly OverlappedPresenter presenter;
    private TrayAnchorPoint anchor;
    private TrayPixelRect workArea;
    private int lastAppliedWidth = -1;
    private int lastAppliedHeight = -1;
    private bool closeRequested;

    /// <summary>
    /// Creates a borderless, top-most flyout window. Each tray open creates a fresh instance; the
    /// flyout closes itself on light-dismiss or when a row action is activated. <paramref name="onAction"/>
    /// receives an activated action row (which dismisses the flyout), while <paramref name="onToggle"/>
    /// receives an activated permission toggle row (which keeps the flyout open).
    /// </summary>
    public TrayFlyoutWindow(Action<TrayFlyoutAction> onAction, Action<TrayFlyoutAction> onToggle)
    {
        this.onAction = onAction;
        this.onToggle = onToggle;
        this.presenter = OverlappedPresenter.CreateForContextMenu();
        this.presenter.IsAlwaysOnTop = true;
        this.AppWindow.SetPresenter(this.presenter);
        this.Title = "OpenClaw";
        this.Activated += this.OnActivated;
    }

    /// <summary>
    /// Builds the flyout content from the model, themes it, sizes it to content, anchors it near the
    /// tray, and activates it. Called once per instance on the WinUI dispatcher in response to a tray click.
    /// </summary>
    public void ShowFor(TrayFlyoutModel model, WindowsThemePalette palette, TrayAnchorPoint anchor)
    {
        this.anchor = anchor;
        this.workArea = ResolveWorkArea(anchor);
        this.BuildContent(model, palette);
        this.Activate();

        // Size from the row estimate for the first paint; OnContentSizeChanged corrects to the exact,
        // DPI-scaled content size once layout runs.
        this.PlaceAndResize(EstimatePhysicalWidth(1.0), this.EstimateContentHeight(model));
    }

    /// <summary>
    /// Rebuilds the flyout content in place after a permission toggle flips a preference, then re-measures and
    /// re-heights the already-shown window. This swaps only <see cref="Window.Content"/> via the same fresh-tree
    /// <c>BuildContent</c> used on open; it never activates, hides, creates, or closes the window, so it cannot
    /// reintroduce the reparenting or hidden-window-reuse faults the open/close path was hardened against. The
    /// window keeps its current X/Y anchor because a toggle does not move the flyout.
    /// </summary>
    public void Refresh(TrayFlyoutModel model, WindowsThemePalette palette)
    {
        if (this.closeRequested)
        {
            return;
        }

        this.BuildContent(model, palette);
        this.PlaceAndResize(EstimatePhysicalWidth(1.0), this.EstimateContentHeight(model));
    }

    private void BuildContent(TrayFlyoutModel model, WindowsThemePalette palette)
    {
        var background = ToBrush(palette.CardBackgroundColor);
        var stroke = ToBrush(palette.CardStrokeColor);

        // Build a fresh visual tree on every show. The window is reused across opens, so reparenting a
        // persistent panel into a new Border would fault the native XAML layer on the second open.
        var panel = new StackPanel { Spacing = 0, Width = FlyoutContentWidth };

        var first = true;
        foreach (var section in model.Sections)
        {
            if (!first)
            {
                panel.Children.Add(BuildSeparator(palette));
            }

            first = false;
            this.AppendSection(panel, section, palette);
        }

        // A ScrollViewer keeps a tall flyout (many rows on a short screen) scrollable instead of running off the
        // work area or clipping the Exit row; the window height is capped to the work area by the positioner.
        var scroller = new ScrollViewer
        {
            Content = panel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollMode = ScrollMode.Auto,
            HorizontalScrollMode = ScrollMode.Disabled,
        };

        this.Content = new Border
        {
            Background = background,
            BorderBrush = stroke,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(6),
            RequestedTheme = ResolveElementTheme(palette),
            Child = scroller,
        };

        // Re-fit the window to the true, DPI-scaled content height once layout runs. A fresh panel (and thus a
        // fresh subscription) is created on every build, so handlers never accumulate across opens or refreshes.
        panel.SizeChanged += this.OnContentSizeChanged;
    }

    private void AppendSection(StackPanel panel, TrayFlyoutSection section, WindowsThemePalette palette)
    {
        if (!string.IsNullOrWhiteSpace(section.Heading))
        {
            panel.Children.Add(new TextBlock
            {
                Text = section.Heading,
                FontSize = 11,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = ToBrush(palette.TextSecondaryColor),
                Margin = new Thickness(8, 6, 8, 2),
            });
        }

        foreach (var statusRow in section.StatusRows)
        {
            panel.Children.Add(BuildStatusRow(statusRow, palette));
        }

        foreach (var actionRow in section.ActionRows)
        {
            panel.Children.Add(this.BuildActionRow(actionRow, palette));
        }

        foreach (var toggleRow in section.ToggleRows)
        {
            panel.Children.Add(this.BuildToggleRow(toggleRow, palette));
        }
    }

    private static FrameworkElement BuildStatusRow(TrayStatusRow row, WindowsThemePalette palette)
    {
        var content = new StackPanel
        {
            Orientation = XamlOrientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = XamlVerticalAlignment.Center,
        };

        content.Children.Add(new Ellipse
        {
            Width = 8,
            Height = 8,
            Fill = ToBrush(ResolveToneColor(row.Tone, palette)),
            VerticalAlignment = XamlVerticalAlignment.Center,
        });

        var labels = new StackPanel { Spacing = 0 };
        labels.Children.Add(new TextBlock
        {
            Text = row.Label,
            FontSize = 13,
            Foreground = ToBrush(palette.TextPrimaryColor),
        });
        if (!string.IsNullOrWhiteSpace(row.Detail))
        {
            labels.Children.Add(new TextBlock
            {
                Text = row.Detail,
                FontSize = 11,
                Foreground = ToBrush(palette.TextSecondaryColor),
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
        }

        content.Children.Add(labels);

        var grid = BuildRowGrid(content);
        if (!string.IsNullOrWhiteSpace(row.Badge))
        {
            grid.Children.Add(BuildBadge(row.Badge!, palette));
        }

        grid.Padding = new Thickness(8, 6, 8, 6);
        return grid;
    }

    private FrameworkElement BuildActionRow(TrayActionRow row, WindowsThemePalette palette)
    {
        var content = new StackPanel
        {
            Orientation = XamlOrientation.Horizontal,
            Spacing = 10,
            VerticalAlignment = XamlVerticalAlignment.Center,
        };

        content.Children.Add(new FontIcon
        {
            Glyph = row.Glyph,
            FontSize = 15,
            Foreground = ToBrush(palette.TextSecondaryColor),
            VerticalAlignment = XamlVerticalAlignment.Center,
        });
        content.Children.Add(new TextBlock
        {
            Text = row.Label,
            FontSize = 13,
            Foreground = ToBrush(palette.TextPrimaryColor),
            VerticalAlignment = XamlVerticalAlignment.Center,
        });

        var grid = BuildRowGrid(content);
        if (!string.IsNullOrWhiteSpace(row.Badge))
        {
            grid.Children.Add(BuildBadge(row.Badge!, palette));
        }

        var button = new XamlButton
        {
            Content = grid,
            HorizontalAlignment = XamlHorizontalAlignment.Stretch,
            HorizontalContentAlignment = XamlHorizontalAlignment.Stretch,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 6, 8, 6),
        };
        ApplyRowButtonChrome(button, palette);
        button.Click += (_, _) => this.Invoke(row.Action);
        return button;
    }

    private FrameworkElement BuildToggleRow(TrayToggleRow row, WindowsThemePalette palette)
    {
        var content = new StackPanel
        {
            Orientation = XamlOrientation.Horizontal,
            Spacing = 10,
            VerticalAlignment = XamlVerticalAlignment.Center,
        };

        content.Children.Add(new FontIcon
        {
            Glyph = row.Glyph,
            FontSize = 15,
            Foreground = ToBrush(palette.TextSecondaryColor),
            VerticalAlignment = XamlVerticalAlignment.Center,
        });
        content.Children.Add(new TextBlock
        {
            Text = row.Label,
            FontSize = 13,
            Foreground = ToBrush(palette.TextPrimaryColor),
            VerticalAlignment = XamlVerticalAlignment.Center,
        });

        var grid = BuildRowGrid(content);

        // A right-aligned check FontIcon reads compactly: accent-tinted when on, dim when off. The whole row is a
        // button so the hit target stays large; activating it routes through the toggle channel, which never closes
        // the flyout (unlike Invoke), so the user can flip several capabilities while the menu stays open.
        var indicator = new FontIcon
        {
            Glyph = ToggleIndicatorGlyph,
            FontSize = 16,
            Foreground = ToBrush(row.IsOn ? palette.AccentColor : palette.TextSecondaryColor),
            Opacity = row.IsOn ? 1.0 : 0.4,
            HorizontalAlignment = XamlHorizontalAlignment.Right,
            VerticalAlignment = XamlVerticalAlignment.Center,
        };
        Grid.SetColumn(indicator, 1);
        grid.Children.Add(indicator);

        var button = new XamlButton
        {
            Content = grid,
            HorizontalAlignment = XamlHorizontalAlignment.Stretch,
            HorizontalContentAlignment = XamlHorizontalAlignment.Stretch,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 6, 8, 6),
        };
        ApplyRowButtonChrome(button, palette);
        button.Click += (_, _) => this.InvokeToggle(row.ToggleAction);
        return button;
    }

    private static Grid BuildRowGrid(FrameworkElement leadingContent)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(leadingContent, 0);
        grid.Children.Add(leadingContent);
        return grid;
    }

    private static FrameworkElement BuildBadge(string text, WindowsThemePalette palette)
    {
        var badge = new Border
        {
            Background = ToBrush(palette.AccentColor),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(7, 1, 7, 1),
            HorizontalAlignment = XamlHorizontalAlignment.Right,
            VerticalAlignment = XamlVerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = text,
                FontSize = 11,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = ToBrush(palette.AccentTextColor),
            },
        };
        Grid.SetColumn(badge, 1);
        return badge;
    }

    private static FrameworkElement BuildSeparator(WindowsThemePalette palette)
    {
        return new Border
        {
            Height = 1,
            Background = ToBrush(palette.CardStrokeColor),
            Margin = new Thickness(8, 4, 8, 4),
            Opacity = 0.6,
        };
    }

    private void Invoke(TrayFlyoutAction action)
    {
        if (this.closeRequested)
        {
            return;
        }

        // Close the flyout before routing: the action may activate the main window, which would
        // otherwise re-enter this window's Deactivated handler.
        this.RequestClose();
        this.onAction(action);
    }

    private void InvokeToggle(TrayFlyoutAction action)
    {
        if (this.closeRequested)
        {
            return;
        }

        // Deliberately does NOT call RequestClose: the user expects to flip a capability and keep the menu open.
        // Clicking a button inside the flyout keeps it focused, so there is no light-dismiss race here. The handler
        // persists the preference and calls Refresh, which rebuilds Content in place without re-activating.
        this.onToggle(action);
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            this.RequestClose();
        }
    }

    /// <summary>
    /// Closes the flyout exactly once. Closing is posted back to the dispatcher so it never runs inside
    /// the activation callback, which otherwise faults the windowing layer on repeated open/close cycles.
    /// </summary>
    public void RequestClose()
    {
        if (this.closeRequested)
        {
            return;
        }

        this.closeRequested = true;
        this.Activated -= this.OnActivated;
        if (!this.DispatcherQueue.TryEnqueue(this.Close))
        {
            this.Close();
        }
    }

    /// <summary>
    /// Resizes and repositions the window for a content size given in physical pixels, clamped to the work area
    /// by the positioner. No-ops when the resulting size is unchanged so a settle-driven re-measure cannot loop.
    /// </summary>
    private void PlaceAndResize(int physicalWidth, int physicalHeight)
    {
        if (this.closeRequested)
        {
            return;
        }

        var placement = TrayFlyoutPositioner.Place(this.workArea, this.anchor.X, this.anchor.Y, physicalWidth, physicalHeight);
        if (placement.Width == this.lastAppliedWidth && placement.Height == this.lastAppliedHeight)
        {
            return;
        }

        this.lastAppliedWidth = placement.Width;
        this.lastAppliedHeight = placement.Height;
        this.AppWindow.MoveAndResize(new RectInt32(placement.X, placement.Y, placement.Width, placement.Height));
    }

    /// <summary>
    /// Once the content has laid out, re-fits the window to its exact height scaled by the display's rasterization
    /// scale, so the flyout hugs its content on any DPI instead of relying on the row-count estimate.
    /// </summary>
    private void OnContentSizeChanged(object sender, SizeChangedEventArgs args)
    {
        if (this.closeRequested || sender is not FrameworkElement content)
        {
            return;
        }

        var scale = content.XamlRoot?.RasterizationScale ?? 1.0;
        var physicalWidth = EstimatePhysicalWidth(scale);
        var physicalHeight = (int)Math.Ceiling((content.ActualHeight + ChromeThickness) * scale);
        this.PlaceAndResize(physicalWidth, physicalHeight);
    }

    private static int EstimatePhysicalWidth(double scale)
    {
        return (int)Math.Ceiling((FlyoutContentWidth + ChromeThickness) * scale);
    }

    /// <summary>
    /// Estimates the natural pixel height from the row counts so the window can be sized before layout settles.
    /// </summary>
    private int EstimateContentHeight(TrayFlyoutModel model)
    {
        const int statusRowHeight = 38;
        const int actionRowHeight = 36;
        const int separatorHeight = 9;
        const int chromePadding = 14;

        var height = chromePadding;
        var sectionIndex = 0;
        foreach (var section in model.Sections)
        {
            if (sectionIndex > 0)
            {
                height += separatorHeight;
            }

            sectionIndex++;
            if (!string.IsNullOrWhiteSpace(section.Heading))
            {
                height += 22;
            }

            foreach (var statusRow in section.StatusRows)
            {
                height += string.IsNullOrWhiteSpace(statusRow.Detail) ? statusRowHeight : statusRowHeight + 14;
            }

            height += section.ActionRows.Count * actionRowHeight;
            height += section.ToggleRows.Count * actionRowHeight;
        }

        return height;
    }

    private static TrayPixelRect ResolveWorkArea(TrayAnchorPoint anchor)
    {
        var display = DisplayArea.GetFromPoint(
            new PointInt32(anchor.X, anchor.Y),
            DisplayAreaFallback.Primary);
        var work = display.WorkArea;
        return new TrayPixelRect(work.X, work.Y, work.Width, work.Height);
    }

    private static Color ResolveToneColor(TrayStatusTone tone, WindowsThemePalette palette)
    {
        return tone switch
        {
            TrayStatusTone.Success => palette.SuccessColor,
            TrayStatusTone.Caution => palette.CautionColor,
            TrayStatusTone.Critical => palette.CriticalColor,
            TrayStatusTone.Accent => palette.AccentColor,
            _ => palette.TextSecondaryColor,
        };
    }

    private static SolidColorBrush ToBrush(Color color)
    {
        return new SolidColorBrush(color);
    }

    /// <summary>
    /// Styles a row as a menu item via lightweight styling: transparent at rest, a themed layer fill on
    /// hover and a stronger fill on press, so the active row is clearly highlighted without a heavy slab.
    /// </summary>
    private static void ApplyRowButtonChrome(XamlButton button, WindowsThemePalette palette)
    {
        var transparent = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        button.Resources["ButtonBackground"] = transparent;
        button.Resources["ButtonBackgroundPointerOver"] = ToBrush(palette.LayerFillColor);
        button.Resources["ButtonBackgroundPressed"] = ToBrush(palette.CardStrokeColor);
        button.Resources["ButtonBackgroundDisabled"] = transparent;
        button.Resources["ButtonBorderBrush"] = transparent;
        button.Resources["ButtonBorderBrushPointerOver"] = transparent;
        button.Resources["ButtonBorderBrushPressed"] = transparent;
        button.Resources["ButtonBorderBrushDisabled"] = transparent;
    }

    /// <summary>
    /// Picks the element theme that matches the resolved palette so default control chrome (scrollbar, row
    /// hover states) renders legibly against the flyout background regardless of the system theme.
    /// </summary>
    private static ElementTheme ResolveElementTheme(WindowsThemePalette palette)
    {
        return RelativeLuminance(palette.AppBackgroundColor) < 0.5 ? ElementTheme.Dark : ElementTheme.Light;
    }

    private static double RelativeLuminance(Color color)
    {
        static double Channel(byte value)
        {
            var normalized = value / 255.0;
            return normalized <= 0.03928
                ? normalized / 12.92
                : Math.Pow((normalized + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Channel(color.R)) + (0.7152 * Channel(color.G)) + (0.0722 * Channel(color.B));
    }
}
