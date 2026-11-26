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
        Closed += (_, _) => { _timer?.Stop(); CloseGhost(); };

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
        int w = (int)(Width * scale), h = (int)(Height * scale);
        var (x, y) = MonitorService.Resolve(_folder);

        // Align to the real desktop icon grid (free slot nearest to the saved position).
        var (pos, cell) = Snap(DesktopGrid.Get(), x + w / 2, y + h / 2, w, h);
        Claims[_folder.Id] = cell;
        DesktopHost.MoveScreen(_hwnd, pos.X, pos.Y, w, h);

        // Persist the aligned position, but never overwrite one whose monitor is currently missing.
        bool monitorKnown = string.IsNullOrEmpty(_folder.MonitorDeviceName)
                            || MonitorService.ByName(_folder.MonitorDeviceName) != null;
        if (monitorKnown && (pos.X != x || pos.Y != y))
        {
            MonitorService.Store(_folder, pos.X, pos.Y, w, h);
            _snapDirty = true;
        }
    }

    // ---- snapping to the real desktop icon grid ----

    static readonly Dictionary<string, DesktopGrid.Cell> Claims = new();
    static bool _snapDirty;

    /// <summary>Forget claimed grid cells (call before rebuilding all docks).</summary>
    public static void ResetClaims() => Claims.Clear();

    /// <summary>True once if loading moved any dock onto the grid; the caller then saves the profile.</summary>
    public static bool TakeSnapDirty() { var d = _snapDirty; _snapDirty = false; return d; }

    DesktopGrid.Info _grid;
    DragGhostWindow? _ghost;
    (int X, int Y) _snapPos;
    DesktopGrid.Cell _snapCell;

    /// <summary>Nearest free grid slot for a dock window whose center is (cx, cy).</summary>
    ((int X, int Y) Pos, DesktopGrid.Cell Cell) Snap(DesktopGrid.Info g, int cx, int cy, int w, int h)
    {
        var wa = MonitorService.FromPoint(cx, cy).WorkingArea;
        var taken = Claims.Where(kv => kv.Key != _folder.Id).Select(kv => kv.Value).ToHashSet();
        var cell = DesktopGrid.Nearest(g, cx, cy, w, h, wa, taken);
        return (DesktopGrid.WindowPos(g, cell, w, h), cell);
    }

    void UpdateGhost(int dx, int dy)
    {
        int w = _startRect.Right - _startRect.Left, h = _startRect.Bottom - _startRect.Top;
        int x = _startRect.Left + dx, y = _startRect.Top + dy;
        var (pos, cell) = Snap(_grid, x + w / 2, y + h / 2, w, h);
        _snapPos = pos;
        _snapCell = cell;
        if (_ghost == null)
        {
            _ghost = new DragGhostWindow(pos.X, pos.Y, w, h);
            _ghost.Show();
        }
        else _ghost.MoveTo(pos.X, pos.Y, w, h);
        DesktopHost.Reinsert(_hwnd); // keep the dragged icon above its shadow
    }

    void CloseGhost()
    {
        _ghost?.Close();
        _ghost = null;
    }

    // ---- drag to move / click to expand ----

    void OnDown(object sender, MouseButtonEventArgs e)
    {
        _down = true;
        _dragging = false;
        GetCursorPos(out _startCursor);
        GetWindowRect(_hwnd, out _startRect);
        _grid = DesktopGrid.Get();
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
            UpdateGhost(dx, dy);
        }
    }

    void OnUp(object sender, MouseButtonEventArgs e)
    {
        if (!_down) return;
        _down = false;
        Root.ReleaseMouseCapture();

        if (_dragging)
        {
            // Drop onto the slot shown by the shadow.
            int w = _startRect.Right - _startRect.Left, h = _startRect.Bottom - _startRect.Top;
            // Recompute from the real cursor: the last mouse-move event can lag behind the release.
            GetCursorPos(out var cur);
            var (pos, cell) = Snap(_grid, _startRect.Left + (cur.X - _startCursor.X) + w / 2,
                                   _startRect.Top + (cur.Y - _startCursor.Y) + h / 2, w, h);
            _snapPos = pos;
            _snapCell = cell;
            CloseGhost();
            DesktopHost.MoveScreen(_hwnd, _snapPos.X, _snapPos.Y, w, h);
            Claims[_folder.Id] = _snapCell;
            MonitorService.Store(_folder, _snapPos.X, _snapPos.Y, w, h);
            AppState.SaveProfile();
            _dragging = false;
            return;
        }
        TogglePanel();
    }

    // ---- expanded panel ----

    /// <summary>Opens the folder panel if closed (used by DESKLAUNCHER_OPEN for screenshots).</summary>
    public void ShowPanel()
    {
        if (_panel == null) TogglePanel();
    }

    void TogglePanel()
    {
        if (_panel != null) { ClosePanel(); return; }
        // The panel closes itself on deactivation when the user clicks this icon; do not reopen it.
        if ((DateTime.UtcNow - _panelClosedAt).TotalMilliseconds < 250) return;

        FrameworkElement g = CustomGlyph.Visibility == Visibility.Visible ? CustomGlyph : Glyph;
        var c = g.PointToScreen(new Point(g.ActualWidth / 2, g.ActualHeight / 2));
        _panel = new ExpandedPanelWindow(_folder, new POINT { X = (int)c.X, Y = (int)c.Y }, _folder.PanelStyle);
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
        TaskbarToggle.IsChecked = _folder.ShowOnTaskbar;
    }

    void TaskbarToggle_Click(object sender, RoutedEventArgs e) =>
        AppState.SetFolderLocation(_folder, true, TaskbarToggle.IsChecked);

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
        if (MessageBox.Show(Loc.T("ConfirmDeleteFolder", _folder.Name), "DeskLauncher",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            AppState.RemoveFolder(_folder);
    }
}
