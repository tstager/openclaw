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
/// It renders a <see cref="TrayFlyoutModel"/> with status-dot rows, separators, and icon action rows,
/// anchors itself near the tray icon, and closes when it loses focus. Sessions 3-6 extend the model
/// rather than this view.
/// </summary>
public sealed class TrayFlyoutWindow : Window
{
    private const int FlyoutWidth = 300;
    private readonly Action<TrayFlyoutAction> onAction;
    private readonly OverlappedPresenter presenter;
    private bool closeRequested;

    /// <summary>
    /// Creates a borderless, top-most flyout window. Each tray open creates a fresh instance; the
    /// flyout closes itself on light-dismiss or when a row action is activated. <paramref name="onAction"/>
    /// receives the activated row action.
    /// </summary>
    public TrayFlyoutWindow(Action<TrayFlyoutAction> onAction)
    {
        this.onAction = onAction;
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
        this.BuildContent(model, palette);
        this.Activate();

        var measured = this.MeasureContentHeight(model);
        var workArea = ResolveWorkArea(anchor);
        var placement = TrayFlyoutPositioner.Place(workArea, anchor.X, anchor.Y, FlyoutWidth, measured);
        this.AppWindow.MoveAndResize(new RectInt32(placement.X, placement.Y, placement.Width, placement.Height));
    }

    private void BuildContent(TrayFlyoutModel model, WindowsThemePalette palette)
    {
        var background = ToBrush(palette.CardBackgroundColor);
        var stroke = ToBrush(palette.CardStrokeColor);

        // Build a fresh visual tree on every show. The window is reused across opens, so reparenting a
        // persistent panel into a new Border would fault the native XAML layer on the second open.
        var panel = new StackPanel { Spacing = 0 };

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

        this.Content = new Border
        {
            Background = background,
            BorderBrush = stroke,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(6),
            Child = panel,
        };
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
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 6, 8, 6),
        };
        button.Click += (_, _) => this.Invoke(row.Action);
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
    /// Estimates the natural pixel height from the row counts so the window can be sized before layout settles.
    /// </summary>
    private int MeasureContentHeight(TrayFlyoutModel model)
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
}
