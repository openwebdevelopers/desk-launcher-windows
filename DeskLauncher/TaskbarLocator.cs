using System.Windows.Automation;

namespace DeskLauncher;

/// <summary>
/// Finds taskbar buttons through UI Automation, which reports their live screen rectangles on
/// Windows 10/11. Used to place the folder strip right after the app buttons.
/// Best effort: on failure it returns false and the caller falls back to the tray placement.
/// </summary>
public static class TaskbarLocator
{
    static AutomationElement? _tray;

    static AutomationElement? Tray()
    {
        try
        {
            return _tray ??= AutomationElement.RootElement.FindFirst(TreeScope.Children,
                new PropertyCondition(AutomationElement.ClassNameProperty, "Shell_TrayWnd"));
        }
        catch { return null; }
    }

    /// <summary>Right edge (physical pixels) of the rightmost Start / Task View / Search / app button.</summary>
    public static bool TryGetButtonsRight(out int right)
    {
        right = 0;
        try
        {
            var tray = Tray();
            if (tray == null) return false;
            var buttons = tray.FindAll(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));
            foreach (AutomationElement b in buttons)
            {
                var id = b.Current.AutomationId ?? "";
                if (id is not ("StartButton" or "TaskViewButton" or "SearchButton") && !id.StartsWith("Appid:")) continue;
                var r = b.Current.BoundingRectangle;
                if (r.IsEmpty || r.Width <= 0 || double.IsInfinity(r.X)) continue;
                if ((int)r.Right > right) right = (int)r.Right;
            }
            return right > 0;
        }
        catch { _tray = null; return false; } // explorer restarted: look it up again next time
    }
}
