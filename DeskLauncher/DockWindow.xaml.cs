using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using static DeskLauncher.NativeMethods;

namespace DeskLauncher;

public partial class DockWindow : Window
{
    readonly FolderConfig _folder;
    IntPtr _hwnd;
    ExpandedPanelWindow? _panel;
    DateTime _panelClosedAt = DateTime.MinValue;
    DispatcherTimer? _timer;

    bool _down, _dragging;
    POINT _startCursor;
    RECT _startRect;

    public FolderConfig Folder => _folder;

    public DockWindow(FolderConfig folder)
    {
        InitializeComponent();
        _folder = folder;
        Refresh();

        SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            DesktopHost.Attach(_hwnd);
            Reposition();
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _timer.Tick += (_, _) => DesktopHost.EnsureAttached(_hwnd);
            _timer.Start();
        };
        Closed += (_, _) => _timer?.Stop();

        Root.MouseLeftButtonDown += OnDown;
        Root.MouseMove += OnMove;
        Root.MouseLeftButtonUp += OnUp;
        Drop += OnDrop;
        DragOver += (_, e) =>
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        };
    }

    public void Refresh()
    {
        NameText.Text = _folder.Name;
        var img = AppState.Icons.GetFolder(_folder);
        CustomGlyph.Source = img;
        CustomGlyph.Visibility = img != null ? Visibility.Visible : Visibility.Collapsed;
        Glyph.Visibility = img != null ? Visibility.Collapsed : Visibility.Visible;
    }

    void Reposition()
    {
        double scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        var (x, y) = MonitorService.Resolve(_folder);
        DesktopHost.MoveScreen(_hwnd, x, y, (int)(Width * scale), (int)(Height * scale));
    }

    // ---- drag to move / click to expand ----

    void OnDown(object sender, MouseButtonEventArgs e)
    {
        _down = true;
        _dragging = false;
        GetCursorPos(out _startCursor);
        GetWindowRect(_hwnd, out _startRect);
        Root.CaptureMouse();
    }

    void OnMove(object sender, MouseEventArgs e)
    {
        if (!_down) return;
        GetCursorPos(out var cur);
        int dx = cur.X - _startCursor.X, dy = cur.Y - _startCursor.Y;
        if (!_dragging && Math.Abs(dx) + Math.Abs(dy) > 6)
        {
            _dragging = true;
            ClosePanel();
        }
        if (_dragging)
        {
            DesktopHost.MoveScreen(_hwnd, _startRect.Left + dx, _startRect.Top + dy,
                _startRect.Right - _startRect.Left, _startRect.Bottom - _startRect.Top);
        }
    }

    void OnUp(object sender, MouseButtonEventArgs e)
    {
        if (!_down) return;
        _down = false;
        Root.ReleaseMouseCapture();

        if (_dragging)
        {
            GetWindowRect(_hwnd, out var r);
            MonitorService.Store(_folder, r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
            AppState.SaveProfile();
            _dragging = false;
            return;
        }
        TogglePanel();
    }

    // ---- expanded panel ----

    void TogglePanel()
    {
        if (_panel != null) { ClosePanel(); return; }
        // The panel closes itself on deactivation when the user clicks this icon; do not reopen it.
        if ((DateTime.UtcNow - _panelClosedAt).TotalMilliseconds < 250) return;

        FrameworkElement g = CustomGlyph.Visibility == Visibility.Visible ? CustomGlyph : Glyph;
        var c = g.PointToScreen(new Point(g.ActualWidth / 2, g.ActualHeight / 2));
        _panel = new ExpandedPanelWindow(_folder, new POINT { X = (int)c.X, Y = (int)c.Y });
        _panel.Closed += (_, _) =>
        {
            _panel = null;
            _panelClosedAt = DateTime.UtcNow;
        };
        _panel.Show();
    }

    void ClosePanel() => _panel?.Dismiss();

    // ---- drag & drop of .lnk / .exe / files ----

    void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return;
        foreach (var p in paths)
        {
            var item = ShortcutResolver.FromPath(p);
            if (item != null) AppState.AddShortcut(_folder, item);
        }
        e.Handled = true;
    }

    // ---- context menu ----

    void OpenManager_Click(object sender, RoutedEventArgs e) => ManagerWindow.ShowSingleton();

    void DockMenu_Opened(object sender, RoutedEventArgs e)
    {
        var s = _folder.PanelStyle ?? PanelStyles.Arc;
        StyleArc.IsChecked = s == PanelStyles.Arc;
        StyleRing.IsChecked = s == PanelStyles.Ring;
        StylePhone.IsChecked = s == PanelStyles.Phone;
    }

    void Style_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: string style }) AppState.SetFolderStyle(_folder, style);
    }

    void ChangeIcon_Click(object sender, RoutedEventArgs e)
    {
        var name = IconPickerWindow.Pick(null, _folder.CustomIcon);
        if (name != null) AppState.SetFolderIcon(_folder, name);
    }

    void ResetIcon_Click(object sender, RoutedEventArgs e) => AppState.SetFolderIcon(_folder, null);

    void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show($"¿Eliminar la carpeta '{_folder.Name}'?", "DeskLauncher",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            AppState.RemoveFolder(_folder);
    }
}
