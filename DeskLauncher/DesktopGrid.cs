using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using static DeskLauncher.NativeMethods;

namespace DeskLauncher;

/// <summary>
/// The real desktop icon grid (cell size and origin read from the desktop's SysListView32), so
/// folders can snap to the same slots as the normal desktop icons.
/// </summary>
public static class DesktopGrid
{
    const uint LVM_GETITEMSPACING = 0x1033;
    const int SM_CXICONSPACING = 38, SM_CYICONSPACING = 39;

    [DllImport("user32.dll")]
    static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    static extern int GetSystemMetrics(int index);

    public readonly record struct Info(int CellW, int CellH, int OriginX, int OriginY);
    /// <summary>A grid cell, identified by its absolute top-left in screen pixels.</summary>
    public readonly record struct Cell(int X, int Y);

    /// <summary>Current grid in screen pixels. Falls back to system icon-spacing metrics.</summary>
    public static Info Get()
    {
        int cw = 0, ch = 0;
        int ox = System.Windows.Forms.SystemInformation.VirtualScreen.Left;
        int oy = System.Windows.Forms.SystemInformation.VirtualScreen.Top;

        var host = DesktopHost.FindHost();
        var view = host == IntPtr.Zero ? IntPtr.Zero : FindWindowEx(host, IntPtr.Zero, "SHELLDLL_DefView", null);
        var list = view == IntPtr.Zero ? IntPtr.Zero : FindWindowEx(view, IntPtr.Zero, "SysListView32", null);
        if (list != IntPtr.Zero)
        {
            long r = SendMessage(list, LVM_GETITEMSPACING, IntPtr.Zero, IntPtr.Zero).ToInt64();
            cw = (int)(r & 0xFFFF);
            ch = (int)((r >> 16) & 0xFFFF);
            if (GetWindowRect(list, out var rc)) { ox = rc.Left; oy = rc.Top; }
        }
        if (cw <= 0) cw = GetSystemMetrics(SM_CXICONSPACING);
        if (ch <= 0) ch = GetSystemMetrics(SM_CYICONSPACING);
        if (cw <= 0) cw = 75;
        if (ch <= 0) ch = 75;
        return new Info(cw, ch, ox, oy);
    }

    static int FloorDiv(int a, int b) => (int)Math.Floor((double)a / b);

    public static Cell CellAt(Info g, int x, int y) =>
        new(g.OriginX + FloorDiv(x - g.OriginX, g.CellW) * g.CellW,
            g.OriginY + FloorDiv(y - g.OriginY, g.CellH) * g.CellH);
    /// <summary>Top-left (screen px) of a w x h window centered in the cell.</summary>
    public static (int X, int Y) WindowPos(Info g, Cell c, int w, int h) =>
        (c.X + (g.CellW - w) / 2, c.Y + (g.CellH - h) / 2);

    /// <summary>
    /// Nearest free cell to the point. The grid starts at the monitor's work-area corner (like the
    /// real desktop icons, which sit below/beside taskbars and toolbars), and a cell is valid when
    /// the whole cell is inside the work area. <paramref name="taken"/> are cells used by other
    /// folders (absolute screen coordinates, so they are comparable across monitors).
    /// </summary>
    public static Cell Nearest(Info g, int px, int py, int w, int h,
        System.Drawing.Rectangle workArea, ICollection<Cell> taken)
    {
        g = g with { OriginX = workArea.Left, OriginY = workArea.Top };
        var origin = CellAt(g, px, py);
        Cell? best = null;
        for (int ring = 0; ring <= 40 && best == null; ring++)
        {
            double bestD = double.MaxValue;
            for (int dr = -ring; dr <= ring; dr++)
            for (int dc = -ring; dc <= ring; dc++)
            {
                if (Math.Max(Math.Abs(dr), Math.Abs(dc)) != ring) continue;
                var c = new Cell(origin.X + dc * g.CellW, origin.Y + dr * g.CellH);
                if (taken.Contains(c)) continue;
                if (c.X < workArea.Left || c.Y < workArea.Top ||
                    c.X + g.CellW > workArea.Right || c.Y + g.CellH > workArea.Bottom) continue;
                var (x, y) = WindowPos(g, c, w, h);
                double cx = x + w / 2.0 - px, cy = y + h / 2.0 - py;
                double d = cx * cx + cy * cy;
                if (d < bestD) { bestD = d; best = c; }
            }
        }
        return best ?? origin;
    }
}

/// <summary>Click-through translucent "shadow" showing where a dragged folder will land.</summary>
public sealed class DragGhostWindow : Window
{
    IntPtr _hwnd;

    public DragGhostWindow(int pxX, int pxY, int pxW, int pxH)
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        Topmost = false;
        IsHitTestVisible = false;
        Width = 84; Height = 100;   // DIP; the real size is set in pixels by MoveTo

        Content = new Border
        {
            Margin = new Thickness(4),
            CornerRadius = new CornerRadius(14),
            Background = new SolidColorBrush(Color.FromArgb(0x4D, 0x00, 0x00, 0x00)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0xB3, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1.5),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            { BlurRadius = 12, ShadowDepth = 0, Opacity = 0.5, Color = Colors.Black },
        };

        SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            DesktopHost.Attach(_hwnd);
            // Let mouse events pass through to whatever is underneath.
            long ex = GetWindowLongPtr(_hwnd, GWL_EXSTYLE).ToInt64() | 0x20; // WS_EX_TRANSPARENT
            SetWindowLongPtr(_hwnd, GWL_EXSTYLE, new IntPtr(ex));
            DesktopHost.MoveScreen(_hwnd, pxX, pxY, pxW, pxH);
        };
    }

    public void MoveTo(int x, int y, int w, int h)
    {
        if (_hwnd != IntPtr.Zero) DesktopHost.MoveScreen(_hwnd, x, y, w, h);
    }
}
