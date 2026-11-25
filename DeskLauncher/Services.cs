using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using static DeskLauncher.NativeMethods;

namespace DeskLauncher;

/// <summary>Pins WPF windows onto the desktop layer (child of the window hosting SHELLDLL_DefView).</summary>
public static class DesktopHost
{
    public static IntPtr FindHost()
    {
        var progman = FindWindow("Progman", null);
        if (progman != IntPtr.Zero && FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero)
            return progman;

        IntPtr found = IntPtr.Zero;
        EnumWindows((h, _) =>
        {
            var sb = new StringBuilder(64);
            GetClassName(h, sb, sb.Capacity);
            if (sb.ToString() == "WorkerW" &&
                FindWindowEx(h, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero)
            {
                found = h;
                return false;
            }
            return true;
        }, IntPtr.Zero);

        return found != IntPtr.Zero ? found : progman;
    }

    /// <summary>
    /// Top-level tool window kept directly above the desktop shell window in z-order:
    /// visible over the icons, below every normal app window. No reparenting (child layered
    /// windows under Progman are unreliable on recent Windows 11 builds).
    /// </summary>
    public static bool Attach(IntPtr hwnd)
    {
        long ex = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
        ex |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(ex));
        SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
        Reinsert(hwnd);
        return FindHost() != IntPtr.Zero;
    }

    /// <summary>Put the window right above the desktop host (call periodically; Win+D reshuffles).</summary>
    public static void Reinsert(IntPtr hwnd)
    {
        var host = FindHost();
        if (host == IntPtr.Zero) return;
        SetWindowPos(hwnd, host, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    public static void EnsureAttached(IntPtr hwnd) => Reinsert(hwnd);

    /// <summary>Move/size using screen pixel coordinates.</summary>
    public static void MoveScreen(IntPtr hwnd, int x, int y, int w, int h)
    {
        var host = FindHost();
        SetWindowPos(hwnd, host == IntPtr.Zero ? IntPtr.Zero : host, x, y, w, h,
            SWP_NOACTIVATE | (host == IntPtr.Zero ? SWP_NOZORDER : 0));
    }
}

public static class MonitorService
{
    public static System.Windows.Forms.Screen? ByName(string? name) =>
        string.IsNullOrEmpty(name) ? null
        : System.Windows.Forms.Screen.AllScreens.FirstOrDefault(s => s.DeviceName == name);

    public static System.Windows.Forms.Screen FromPoint(int x, int y) =>
        System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(x, y));

    /// <summary>
    /// Screen-pixel position for a folder. If its saved monitor is missing, falls back to
    /// the primary monitor for display only; the saved position is never overwritten here.
    /// </summary>
    public static (int X, int Y) Resolve(FolderConfig f)
    {
        var s = ByName(f.MonitorDeviceName) ?? System.Windows.Forms.Screen.PrimaryScreen
                ?? System.Windows.Forms.Screen.AllScreens[0];
        int x = s.Bounds.X + f.OffsetX;
        int y = s.Bounds.Y + f.OffsetY;
        // Keep visible without persisting.
        x = Math.Clamp(x, s.WorkingArea.Left, Math.Max(s.WorkingArea.Left, s.WorkingArea.Right - 80));
        y = Math.Clamp(y, s.WorkingArea.Top, Math.Max(s.WorkingArea.Top, s.WorkingArea.Bottom - 96));
        return (x, y);
    }

    /// <summary>Stores a screen-pixel position relative to the monitor under it.</summary>
    public static void Store(FolderConfig f, int x, int y, int w, int h)
    {
        var s = FromPoint(x + w / 2, y + h / 2);
        f.MonitorDeviceName = s.DeviceName;
        f.OffsetX = x - s.Bounds.X;
        f.OffsetY = y - s.Bounds.Y;
    }
}

public static class ShortcutResolver
{
    public static ShortcutItem? FromPath(string path)
    {
        if (Directory.Exists(path))
            return new ShortcutItem { Name = Path.GetFileName(path.TrimEnd('\\')), TargetPath = path };
        if (!File.Exists(path)) return null;

        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext == ".lnk") return FromLnk(path);

        string name = Path.GetFileNameWithoutExtension(path);
        if (ext == ".exe")
        {
            try
            {
                var d = FileVersionInfo.GetVersionInfo(path).FileDescription;
                if (!string.IsNullOrWhiteSpace(d)) name = d.Trim();
            }
            catch { }
        }
        return new ShortcutItem { Name = name, TargetPath = path };
    }

    static ShortcutItem FromLnk(string lnk)
    {
        var item = new ShortcutItem
        {
            Name = Path.GetFileNameWithoutExtension(lnk),
            TargetPath = lnk, // fallback: let the shell open the .lnk (UWP / special shortcuts)
        };
        try
        {
            var t = Type.GetTypeFromProgID("WScript.Shell");
            if (t == null) return item;
            dynamic sh = Activator.CreateInstance(t)!;
            dynamic l = sh.CreateShortcut(lnk);
            string target = l.TargetPath ?? "";
            if (!string.IsNullOrWhiteSpace(target) && (File.Exists(target) || Directory.Exists(target)))
            {
                item.TargetPath = target;
                string args = l.Arguments ?? "";
                if (!string.IsNullOrWhiteSpace(args)) item.Arguments = args;
            }
            string icon = l.IconLocation ?? "";
            var comma = icon.LastIndexOf(',');
            if (comma > 0) icon = icon[..comma];
            if (!string.IsNullOrWhiteSpace(icon) && File.Exists(icon)) item.IconPath = icon;
        }
        catch { }
        return item;
    }
}

public static class LaunchService
{
    public static void Launch(ShortcutItem s)
    {
        try
        {
            var psi = new ProcessStartInfo { UseShellExecute = true };
            if (string.IsNullOrEmpty(s.TargetPath) && !string.IsNullOrEmpty(s.AppUserModelId))
            {
                psi.FileName = "explorer.exe";
                psi.Arguments = "shell:AppsFolder\\" + s.AppUserModelId;
            }
            else
            {
                psi.FileName = s.TargetPath;
                if (!string.IsNullOrEmpty(s.Arguments)) psi.Arguments = s.Arguments;
                var dir = Path.GetDirectoryName(s.TargetPath);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) psi.WorkingDirectory = dir;
            }
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"No se pudo abrir '{s.Name}':\n{ex.Message}", "DeskLauncher");
        }
    }
}

public static class AutoStartService
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ValueName = "DeskLauncher";

    public static void Set(bool enabled)
    {
        using var k = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (k == null) return;
        if (enabled) k.SetValue(ValueName, $"\"{Environment.ProcessPath}\"");
        else k.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
