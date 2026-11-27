using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using static DeskLauncher.NativeMethods;

namespace DeskLauncher;

/// <summary>
/// "Fan" popup: a translucent arc band around the dock icon with the apps laid out along it.
/// Top-level, topmost, closes on deactivation.
/// </summary>
public sealed class ExpandedPanelWindow : Window
{
    const double Radius = 200;       // icon ring radius (DIP)
    const double HalfBand = 62;      // band half thickness
    const double Pad = 10;
    const double TopPad = 8;

    readonly FolderConfig _folder;
    readonly POINT _center;          // screen px, center of the dock glyph
    bool _flipUp;
    double _canvasW, _canvasH, _cx, _cy;

    readonly string _style;

    public ExpandedPanelWindow(FolderConfig folder, POINT centerPx, string? style = null)
    {
        _folder = folder;
        _center = centerPx;
        _style = style ?? folder.PanelStyle ?? PanelStyles.Arc;

        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Topmost = true;

        // Open downward unless there is no room below the dock.
        var screen = MonitorService.FromPoint(centerPx.X, centerPx.Y);
        double scale = 1; // refined in Loaded; approximate with 1 here only for the flip test
        _flipUp = centerPx.Y + (Radius + HalfBand + Pad) > screen.WorkingArea.Bottom;

        Content = BuildContent();
        Deactivated += (_, _) => { if (AppState.Profile.Settings.CollapseOnMouseLeave) Dismiss(); };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Dismiss(); };
        Loaded += OnLoaded;
    }

    bool _closing;

    /// <summary>
    /// Closes once. Closing deactivates the window, whose handler closes it again; a second
    /// Close() while closing throws InvalidOperationException (this crashed the app).
    /// </summary>
    public void Dismiss()
    {
        if (_closing) return;
        _closing = true;
        if (!IsLoaded) { Close(); return; }
        PlayOutro(() => { if (!_closedDone) Close(); });
    }

    bool _closedDone;
    protected override void OnClosed(EventArgs e)
    {
        _closedDone = true;
        base.OnClosed(e);
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        _closing = true;
        base.OnClosing(e);
    }

    Point OnArc(double radius, double angleDeg)
    {
        double a = angleDeg * Math.PI / 180;
        double sign = _flipUp ? -1 : 1;
        return new Point(_cx + radius * Math.Cos(a), _cy + sign * radius * Math.Sin(a));
    }

    static readonly Brush ShellFill = new SolidColorBrush(Color.FromArgb(0xF0, 0x2B, 0x2B, 0x2F));
    static readonly Brush ShellStroke = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));

    static System.Windows.Media.Effects.Effect ShellShadow() =>
        new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 14, ShadowDepth = 2, Opacity = 0.5 };

    static double SystemScale()
    {
        using var g = System.Drawing.Graphics.FromHwnd(IntPtr.Zero);
        return g.DpiX / 96.0;
    }

    UIElement BuildContent() => _style switch
    {
        PanelStyles.Ring => BuildRing(),
        PanelStyles.Phone => BuildPhone(),
        PanelStyles.Box => BuildBox(),
        PanelStyles.List => BuildList(),
        _ => BuildArc(),
    };

    // ---- Ring: full circle of apps around the dock icon, launcher logo in the middle ----

    UIElement BuildRing()
    {
        const double HalfRing = 56, Spacing = 80;
        var items = _folder.Shortcuts.OrderBy(s => s.SortOrder).ToList();
        int n = items.Count;

        // Grow with the app count, but never beyond the screen.
        var wa = MonitorService.FromPoint(_center.X, _center.Y).WorkingArea;
        double scale = SystemScale();
        double maxR = Math.Min(wa.Width, wa.Height) / scale / 2 - HalfRing - Pad;
        double r = Math.Min(Math.Max(105, n * Spacing / (2 * Math.PI)), Math.Max(105, maxR));

        double outer = r + HalfRing, inner = r - HalfRing;
        _canvasW = _canvasH = 2 * outer + 2 * Pad;
        _cx = _cy = _canvasW / 2;   // window clamping in OnLoaded keeps the whole ring on screen

        var canvas = new Canvas { Width = _canvasW, Height = _canvasH };
        var band = new CombinedGeometry(GeometryCombineMode.Exclude,
            new EllipseGeometry(new Point(_cx, _cy), outer, outer),
            new EllipseGeometry(new Point(_cx, _cy), inner, inner));
        canvas.Children.Add(new Path
        {
            Data = band,
            Fill = ShellFill,
            Stroke = ShellStroke,
            StrokeThickness = 1,
            Effect = ShellShadow(),
        });

        // Launcher logo in the center (click to close).
        var logo = new Image
        {
            Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Assets/logo.png")),
            Width = 64, Height = 64, Cursor = Cursors.Hand,
        };
        RenderOptions.SetBitmapScalingMode(logo, BitmapScalingMode.HighQuality);
        logo.MouseLeftButtonUp += (_, _) => Dismiss();
        Canvas.SetLeft(logo, _cx - 32);
        Canvas.SetTop(logo, _cy - 32);
        canvas.Children.Add(logo);

        if (n == 0)
        {
            var hint = new TextBlock { Text = "Arrastra apps al icono", Foreground = Brushes.White };
            Canvas.SetLeft(hint, _cx - 62);
            Canvas.SetTop(hint, _cy - r - 8);
            canvas.Children.Add(hint);
        }

        for (int i = 0; i < n; i++)
        {
            double a = (-90 + 360.0 * i / n) * Math.PI / 180;
            var tile = BuildTile(items[i]);
            Canvas.SetLeft(tile, _cx + r * Math.Cos(a) - tile.Width / 2);
            Canvas.SetTop(tile, _cy + r * Math.Sin(a) - 34);
            canvas.Children.Add(tile);
        }
        return canvas;
    }

    // ---- Phone: rounded rectangle with a grid of apps, like a phone folder ----

    UIElement BuildPhone()
    {
        var items = _folder.Shortcuts.OrderBy(s => s.SortOrder).ToList();
        int cols = Math.Clamp(Math.Min(AppState.Profile.Settings.MaxGridColumns, Math.Max(items.Count, 1)), 1, 8);

        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = _folder.Name,
            Foreground = Brushes.White,
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 10),
        });

        if (items.Count == 0)
        {
            stack.Children.Add(new TextBlock
            {
                Text = "Arrastra apps al icono",
                Foreground = Brushes.White,
                Margin = new Thickness(24, 12, 24, 16),
            });
        }
        else
        {
            var grid = new System.Windows.Controls.Primitives.UniformGrid { Columns = cols };
            foreach (var s in items)
            {
                var tile = BuildTile(s);
                tile.Margin = new Thickness(4);
                grid.Children.Add(tile);
            }
            stack.Children.Add(grid);
        }

        var card = new Border
        {
            CornerRadius = new CornerRadius(28),
            Padding = new Thickness(18, 16, 18, 14),
            Background = ShellFill,
            BorderBrush = ShellStroke,
            BorderThickness = new Thickness(1),
            Effect = ShellShadow(),
            Child = stack,
        };
        var root = new Grid { Margin = new Thickness(Pad) };
        root.Children.Add(card);

        root.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        _canvasW = root.DesiredSize.Width;
        _canvasH = root.DesiredSize.Height;

        // Open below the dock icon, or above it when there is no room.
        const double Below = 56, Above = 40;
        var wa = MonitorService.FromPoint(_center.X, _center.Y).WorkingArea;
        _flipUp = _center.Y + (Below + _canvasH) * SystemScale() > wa.Bottom;
        _cx = _canvasW / 2;
        _cy = _flipUp ? _canvasH + Above : -Below;
        return root;
    }

    // ---- Arc: translucent band around the dock icon (original style) ----

    UIElement BuildArc()
    {
        var items = _folder.Shortcuts.OrderBy(s => s.SortOrder).ToList();
        double outer = Radius + HalfBand;
        double inner = Radius - HalfBand;

        _canvasW = 2 * outer + 2 * Pad;
        _canvasH = outer + Pad + TopPad;
        _cx = _canvasW / 2;
        _cy = _flipUp ? _canvasH - TopPad : TopPad;

        int n = Math.Max(items.Count, 1);
        double step = n > 1 ? Math.Min(26, 150.0 / (n - 1)) : 0;
        double total = step * (n - 1);
        double half = Math.Min(90, total / 2 + 26);   // band half-angle
        double start = 90 - total / 2;                // first item is on the right

        var canvas = new Canvas { Width = _canvasW, Height = _canvasH };

        // Ring sector (a1 -> a2 going right -> bottom -> left when opening downward).
        double a1 = 90 - half, a2 = 90 + half;
        var sweep = _flipUp ? SweepDirection.Counterclockwise : SweepDirection.Clockwise;
        var back = _flipUp ? SweepDirection.Clockwise : SweepDirection.Counterclockwise;
        var fig = new PathFigure { StartPoint = OnArc(outer, a1), IsClosed = true };
        fig.Segments.Add(new ArcSegment(OnArc(outer, a2), new Size(outer, outer), 0, false, sweep, true));
        fig.Segments.Add(new LineSegment(OnArc(inner, a2), true));
        fig.Segments.Add(new ArcSegment(OnArc(inner, a1), new Size(inner, inner), 0, false, back, true));
        var geo = new PathGeometry();
        geo.Figures.Add(fig);

        canvas.Children.Add(new Path
        {
            Data = geo,
            Fill = new SolidColorBrush(Color.FromArgb(0xF0, 0x2B, 0x2B, 0x2F)),
            Stroke = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)),
            StrokeThickness = 1,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            { BlurRadius = 14, ShadowDepth = 2, Opacity = 0.5 },
        });

        if (items.Count == 0)
        {
            var hint = new TextBlock { Text = "Arrastra apps al icono", Foreground = Brushes.White };
            var p = OnArc(Radius, 90);
            Canvas.SetLeft(hint, p.X - 62);
            Canvas.SetTop(hint, p.Y - 8);
            canvas.Children.Add(hint);
        }

        for (int i = 0; i < items.Count; i++)
        {
            var tile = BuildTile(items[i]);
            var p = OnArc(Radius, start + step * i);
            Canvas.SetLeft(tile, p.X - tile.Width / 2);
            Canvas.SetTop(tile, p.Y - 34);
            canvas.Children.Add(tile);
        }

        return canvas;
    }

    StackPanel BuildTile(ShortcutItem s)
    {
        var panel = new StackPanel
        {
            Width = 84,
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
            ToolTip = s.TargetPath,
        };
        panel.Children.Add(new Image { Source = AppState.Icons.GetApp(s), Width = 46, Height = 46 });
        panel.Children.Add(new TextBlock
        {
            Text = s.Name,
            Foreground = Brushes.White,
            FontSize = 11,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            MaxHeight = 30,
            Margin = new Thickness(2, 3, 2, 0),
        });

        Wire(panel, s, new Point(0.5, 0.4));
        return panel;
    }

    /// <summary>Intro zoom, hover zoom, click-to-launch and context menu for one app element.</summary>
    void Wire(FrameworkElement panel, ShortcutItem s, Point origin)
    {
        // Two independent scales: one for the load-in zoom, one for hover (so they never fight).
        var intro = new ScaleTransform(0.4, 0.4);
        var hover = new ScaleTransform(1, 1);
        panel.RenderTransformOrigin = origin;
        panel.RenderTransform = new TransformGroup { Children = { intro, hover } };
        panel.Opacity = 0;
        _intro.Add((panel, intro));

        panel.MouseEnter += (_, _) => AnimateScale(hover, 1.16, 160);
        panel.MouseLeave += (_, _) => AnimateScale(hover, 1.0, 200);
        panel.MouseLeftButtonUp += (_, _) =>
        {
            Dismiss();
            LaunchService.Launch(s);
        };

        var menu = new ContextMenu();
        var remove = new MenuItem { Header = Loc.T("Remove") };
        remove.Click += (_, _) =>
        {
            Dismiss();
            AppState.RemoveShortcut(_folder, s);
        };
        var change = new MenuItem { Header = Loc.T("ChangeIconDots") };
        change.Click += (_, _) =>
        {
            Dismiss(); // panel closes on deactivation anyway; the dialog would trigger that
            var name = IconPickerWindow.Pick(null, s.CustomIcon);
            if (name != null) AppState.SetShortcutIcon(_folder, s, name);
        };
        var reset = new MenuItem { Header = Loc.T("ResetIconMenu") };
        reset.Click += (_, _) =>
        {
            Dismiss();
            AppState.SetShortcutIcon(_folder, s, null);
        };
        menu.Items.Add(change);
        menu.Items.Add(reset);
        menu.Items.Add(remove);
        panel.ContextMenu = menu;
    }

    // ---- Box (taskbar): rounded box with an app grid and a caret pointing at the taskbar icon ----

    UIElement BuildBox()
    {
        var items = _folder.Shortcuts.OrderBy(s => s.SortOrder).ToList();
        int cols = Math.Clamp(Math.Min(5, Math.Max(items.Count, 1)), 1, 8);

        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = _folder.Name,
            Foreground = Brushes.White,
            FontSize = 13,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 10),
        });

        if (items.Count == 0)
        {
            stack.Children.Add(new TextBlock
            {
                Text = "Arrastra apps al icono",
                Foreground = Brushes.White,
                Margin = new Thickness(24, 12, 24, 16),
            });
        }
        else
        {
            var grid = new System.Windows.Controls.Primitives.UniformGrid { Columns = cols };
            foreach (var s in items)
            {
                var tile = BuildTile(s);
                tile.Margin = new Thickness(4);
                grid.Children.Add(tile);
            }
            stack.Children.Add(grid);
        }

        var card = new Border
        {
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(16, 12, 16, 12),
            Background = ShellFill,
            BorderBrush = ShellStroke,
            BorderThickness = new Thickness(1),
            Effect = ShellShadow(),
            Child = stack,
        };
        var caret = new System.Windows.Shapes.Polygon
        {
            Points = new PointCollection { new Point(0, 0), new Point(20, 0), new Point(10, 10) },
            Fill = ShellFill,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, -1, 0, 0),
        };
        var root = new StackPanel { Margin = new Thickness(Pad) };
        root.Children.Add(card);
        root.Children.Add(caret);

        root.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        _canvasW = root.DesiredSize.Width;
        _canvasH = root.DesiredSize.Height;
        _flipUp = true;
        _cx = _canvasW / 2;
        _cy = _canvasH - Pad + 26; // the taskbar icon center sits just under the caret tip
        return root;
    }

    // ---- List (taskbar): labeled rows fanning up from the taskbar icon on a gentle curve ----

    UIElement BuildList()
    {
        const double Step = 58, IconX = 250, IconSize = 46, LabelGap = 10;
        var items = _folder.Shortcuts.OrderBy(s => s.SortOrder).ToList();
        int n = items.Count;

        double Shift(int i) => Math.Min(0.9 * i * i, 120);
        double maxShift = n > 0 ? Shift(n - 1) : 0;

        _canvasW = IconX + maxShift + IconSize / 2 + 2 * Pad;
        _canvasH = Math.Max(n, 1) * Step + 2 * Pad + 10;
        _flipUp = true;
        _cx = IconX + Pad;
        _cy = _canvasH - Pad - 10 - Step / 2 + 58; // first row center + distance down to the taskbar icon

        var canvas = new Canvas { Width = _canvasW, Height = _canvasH };

        if (n == 0)
        {
            var hint = new Border
            {
                Background = ShellFill,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 6, 12, 6),
                Child = new TextBlock { Text = "Arrastra apps al icono", Foreground = Brushes.White },
            };
            Canvas.SetLeft(hint, _cx - 160);
            Canvas.SetTop(hint, _canvasH - Pad - Step);
            canvas.Children.Add(hint);
            return canvas;
        }

        for (int i = 0; i < n; i++)
        {
            double iconCx = _cx + Shift(i);
            double rowCy = _canvasH - Pad - 10 - Step / 2 - i * Step;

            var label = new Border
            {
                Background = ShellFill,
                BorderBrush = ShellStroke,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(10, 4, 10, 4),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, LabelGap, 0),
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new RotateTransform(Math.Min(1.1 * i, 9)),
                Child = new TextBlock
                {
                    Text = items[i].Name,
                    Foreground = Brushes.White,
                    FontSize = 13,
                    MaxWidth = 190,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                },
            };
            var icon = new Image { Source = AppState.Icons.GetApp(items[i]), Width = IconSize, Height = IconSize };
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                ToolTip = items[i].TargetPath,
            };
            row.Children.Add(label);
            row.Children.Add(icon);
            Wire(row, items[i], new Point(0.9, 0.5));

            row.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(row, iconCx - (row.DesiredSize.Width - IconSize / 2));
            Canvas.SetTop(row, rowCy - row.DesiredSize.Height / 2);
            canvas.Children.Add(row);
        }
        return canvas;
    }

    void OnLoaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        double scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        int w = (int)Math.Ceiling(_canvasW * scale);
        int h = (int)Math.Ceiling(_canvasH * scale);

        var wa = MonitorService.FromPoint(_center.X, _center.Y).WorkingArea;
        int x = _center.X - (int)(_cx * scale);
        int y = _center.Y - (int)(_cy * scale);
        x = Math.Clamp(x, wa.Left, Math.Max(wa.Left, wa.Right - w));
        y = Math.Clamp(y, wa.Top, Math.Max(wa.Top, wa.Bottom - h));

        SetWindowPos(hwnd, HWND_TOPMOST, x, y, w, h, 0);
        Activate();

        BeginAnimation(OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140)));
        PlayIntro();
    }

    // ---- animations ----

    readonly List<(UIElement Tile, ScaleTransform Scale)> _intro = new();

    static void AnimateScale(ScaleTransform t, double to, int ms)
    {
        var a = new System.Windows.Media.Animation.DoubleAnimation(to, TimeSpan.FromMilliseconds(ms))
        {
            EasingFunction = new System.Windows.Media.Animation.CubicEase
            { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut },
        };
        t.BeginAnimation(ScaleTransform.ScaleXProperty, a);
        t.BeginAnimation(ScaleTransform.ScaleYProperty, a);
    }

    /// <summary>
    /// Reverse of <see cref="PlayIntro"/>: tiles shrink + fade out, last one first, then the whole
    /// window fades; <paramref name="done"/> runs when it is gone.
    /// </summary>
    void PlayOutro(Action done)
    {
        var ease = new System.Windows.Media.Animation.CubicEase
        { EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn };
        var dur = TimeSpan.FromMilliseconds(200);
        const int fadeMs = 140;
        double lastBegin = 0;

        for (int i = 0; i < _intro.Count; i++)
        {
            var (tile, scale) = _intro[i];
            double beginMs = Math.Min((_intro.Count - 1 - i) * 22, 220); // last tile leaves first
            lastBegin = Math.Max(lastBegin, beginMs);
            var begin = TimeSpan.FromMilliseconds(beginMs);

            var zoom = new System.Windows.Media.Animation.DoubleAnimation(0.4, dur)
            { BeginTime = begin, EasingFunction = ease, FillBehavior = System.Windows.Media.Animation.FillBehavior.HoldEnd };
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, zoom);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, zoom);

            tile.BeginAnimation(OpacityProperty,
                new System.Windows.Media.Animation.DoubleAnimation(0, dur) { BeginTime = begin, EasingFunction = ease });
        }

        var fade = new System.Windows.Media.Animation.DoubleAnimation(0, TimeSpan.FromMilliseconds(fadeMs))
        { BeginTime = TimeSpan.FromMilliseconds(lastBegin + 200 - fadeMs) };
        fade.Completed += (_, _) => done();
        BeginAnimation(OpacityProperty, fade);
    }

    /// <summary>Tiles zoom + fade in one after another (short stagger so it feels smooth, not slow).</summary>
    void PlayIntro()
    {
        var ease = new System.Windows.Media.Animation.CubicEase
        { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut };
        for (int i = 0; i < _intro.Count; i++)
        {
            var (tile, scale) = _intro[i];
            var begin = TimeSpan.FromMilliseconds(Math.Min(i * 28, 280));
            var dur = TimeSpan.FromMilliseconds(320);

            var zoom = new System.Windows.Media.Animation.DoubleAnimation(0.4, 1, dur)
            { BeginTime = begin, EasingFunction = ease };
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, zoom);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, zoom);

            tile.BeginAnimation(OpacityProperty,
                new System.Windows.Media.Animation.DoubleAnimation(0, 1, dur) { BeginTime = begin, EasingFunction = ease });
        }
    }
}
