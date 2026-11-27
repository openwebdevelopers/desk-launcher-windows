using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using static DeskLauncher.NativeMethods;

namespace DeskLauncher;

/// <summary>
/// Strip of folder icons laid over the Windows taskbar, just left of the system tray. Windows has
/// no API to add items to the taskbar, so this is a topmost, click-through-free window that follows
/// the taskbar's rectangle and re-asserts itself above it. Clicking an icon opens its folder with
/// the folder's taskbar style (Box or List).
/// </summary>
public sealed class TaskbarStripWindow : Window
{
    static readonly IntPtr HWND_TOPMOST_ = new(-1);

    readonly List<FolderConfig> _folders;
    // Alpha 1, not 0: a fully transparent pixel of a layered window lets clicks fall through to the taskbar.
    static readonly Brush HitBg = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
    readonly StackPanel _row = new() { Orientation = Orientation.Horizontal, Background = HitBg };
    readonly DispatcherTimer _timer;
    IntPtr _hwnd;
    bool _hiddenForFullScreen;
    ExpandedPanelWindow? _panel;
    FolderConfig? _panelFolder;
    DateTime _panelClosedAt = DateTime.MinValue;

    public TaskbarStripWindow(IEnumerable<FolderConfig> folders)
    {
        _folders = folders.ToList();

        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.Width;
        Topmost = true;
        Content = _row;
        Build();

        SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            long ex = GetWindowLongPtr(_hwnd, GWL_EXSTYLE).ToInt64() | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            SetWindowLongPtr(_hwnd, GWL_EXSTYLE, new IntPtr(ex));
        };
        Loaded += (_, _) => Reposition();
        SizeChanged += (_, _) => Reposition();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _timer.Tick += (_, _) => Reposition();
        _timer.Start();
        Closed += (_, _) => { _timer.Stop(); _panel?.Dismiss(); };
    }

    /// <summary>Rebuilds the icons (names, icons, list of folders) after a change.</summary>
    public void Refresh(IEnumerable<FolderConfig> folders)
    {
        _folders.Clear();
        _folders.AddRange(folders);
        Build();
    }

    // ---- content ----

    static bool TaskbarIsDark()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return k?.GetValue("SystemUsesLightTheme") is not int v || v == 0;
        }
        catch { return true; }
    }

    void Build()
    {
        _row.Children.Clear();
        var fg = TaskbarIsDark() ? Brushes.White : Brushes.Black;
        foreach (var f in _folders)
        {
            var img = AppState.Icons.GetFolder(f);
            UIElement face = img != null
                ? new Image { Source = img, Width = 26, Height = 26 }
                : new TextBlock
                {
                    Text = "",
                    FontFamily = (FontFamily)Application.Current.Resources["IconFont"],
                    FontSize = 20,
                    Foreground = fg,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                };

            var hoverBg = new SolidColorBrush(Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF));
            var item = new Border
            {
                Width = 44,
                Margin = new Thickness(2, 4, 2, 4),
                CornerRadius = new CornerRadius(6),
                Background = HitBg,
                Cursor = Cursors.Hand,
                ToolTip = f.Name,
                AllowDrop = true,
                Child = face,
            };
            item.MouseEnter += (_, _) => item.Background = hoverBg;
            item.MouseLeave += (_, _) => item.Background = HitBg;
            item.MouseLeftButtonUp += (_, _) => TogglePanel(f, item);
            item.DragOver += (_, e) =>
            {
                e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
                e.Handled = true;
            };
            item.Drop += (_, e) =>
            {
                if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
                    foreach (var p in paths)
                        if (ShortcutResolver.FromPath(p) is { } s) AppState.AddShortcut(f, s);
                e.Handled = true;
            };
            item.ContextMenu = BuildMenu(f);
            _row.Children.Add(item);
        }
    }

    ContextMenu BuildMenu(FolderConfig f)
    {
        var menu = new ContextMenu();
        var open = new MenuItem { Header = Loc.T("OpenManager") };
        open.Click += (_, _) => ManagerWindow.ShowSingleton();

        var style = new MenuItem { Header = Loc.T("TaskbarStyle") };
        MenuItem Opt(string header, string key)
        {
            var m = new MenuItem { Header = header, IsCheckable = true, Tag = key };
            m.Click += (_, _) => AppState.SetTaskbarStyle(f, key);
            return m;
        }
        var box = Opt(Loc.T("StyleBox"), PanelStyles.Box);
        var list = Opt(Loc.T("StyleList"), PanelStyles.List);
        style.Items.Add(box);
        style.Items.Add(list);

        var desktop = new MenuItem { Header = Loc.T("ShowOnDesktop"), IsCheckable = true };
        desktop.Click += (_, _) => AppState.SetFolderLocation(f, desktop.IsChecked, true);

        var remove = new MenuItem { Header = Loc.T("RemoveFromTaskbar") };
        remove.Click += (_, _) => AppState.SetFolderLocation(f, f.ShowOnDesktop, false);

        menu.Opened += (_, _) =>
        {
            var s = f.TaskbarStyle ?? PanelStyles.Box;
            box.IsChecked = s == PanelStyles.Box;
            list.IsChecked = s == PanelStyles.List;
            desktop.IsChecked = f.ShowOnDesktop;
        };
        menu.Items.Add(open);
        menu.Items.Add(style);
        menu.Items.Add(desktop);
        menu.Items.Add(remove);
        return menu;
    }

    // ---- panel ----

    void TogglePanel(FolderConfig f, FrameworkElement anchor)
    {
        if (_panel != null)
        {
            var same = _panelFolder == f;
            _panel.Dismiss();
            if (same) return;
        }
        if ((DateTime.UtcNow - _panelClosedAt).TotalMilliseconds < 250) return;

        var c = anchor.PointToScreen(new Point(anchor.ActualWidth / 2, anchor.ActualHeight / 2));
        _panelFolder = f;
        _panel = new ExpandedPanelWindow(f, new POINT { X = (int)c.X, Y = (int)c.Y }, f.TaskbarStyle ?? PanelStyles.Box);
        _panel.Closed += (_, _) =>
        {
            _panel = null;
            _panelFolder = null;
            _panelClosedAt = DateTime.UtcNow;
        };
        _panel.Show();
    }

    // ---- placement over the taskbar ----

    void Reposition()
    {
        if (_hwnd == IntPtr.Zero || !IsLoaded) return;

        // A full-screen app (video, game, presentation) hides the taskbar: stay out of its way.
        bool fullScreen = SHQueryUserNotificationState(out var qs) == 0 && qs is 2 or 3 or 4;
        if (fullScreen)
        {
            if (_hiddenForFullScreen) return;
            _hiddenForFullScreen = true;
            _panel?.Dismiss();
            ShowWindow(_hwnd, 0); // SW_HIDE
            return;
        }
        if (_hiddenForFullScreen)
        {
            _hiddenForFullScreen = false;
            ShowWindow(_hwnd, 4); // SW_SHOWNOACTIVATE
        }

        double scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        int w = (int)Math.Ceiling(ActualWidth * scale);

        var tray = FindWindow("Shell_TrayWnd", null);
        int x, y, h;
        if (tray != IntPtr.Zero && GetWindowRect(tray, out var tb))
        {
            bool horizontal = (tb.Right - tb.Left) >= (tb.Bottom - tb.Top);
            if (horizontal)
            {
                h = tb.Bottom - tb.Top;
                y = tb.Top;
                int right = tb.Right - 220; // fallback if the tray rectangle is unknown
                var notify = FindWindowEx(tray, IntPtr.Zero, "TrayNotifyWnd", null);
                if (notify != IntPtr.Zero && GetWindowRect(notify, out var nr) && nr.Right > nr.Left)
                    right = nr.Left;
                x = right - w - 8;
            }
            else
            {
                // Vertical taskbar: float a bar next to its bottom end.
                h = (int)(48 * scale);
                x = tb.Left;
                y = tb.Bottom - h - 140;
            }
        }
        else
        {
            var scr = System.Windows.Forms.Screen.PrimaryScreen ?? System.Windows.Forms.Screen.AllScreens[0];
            h = scr.Bounds.Bottom - scr.WorkingArea.Bottom;
            if (h <= 0) h = (int)(48 * scale);
            y = scr.Bounds.Bottom - h;
            x = scr.Bounds.Right - w - 240;
        }

        double hDip = h / scale;
        if (Math.Abs(Height - hDip) > 0.5) Height = hDip;
        SetWindowPos(_hwnd, HWND_TOPMOST_, x, y, w, h, SWP_NOACTIVATE); // also re-raises above the taskbar
    }
}
