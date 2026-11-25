using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace DeskLauncher;

public static class ThemeNames
{
    public const string System = "System";
    public const string Light = "Light";
    public const string Dark = "Dark";
}

/// <summary>
/// Light / dark / follow-system theming. Switches the WPF Fluent theme and recolors the shared
/// brushes in App.xaml in place (same brush instances, so every StaticResource user updates).
/// </summary>
public static class ThemeService
{
    sealed record Palette(
        string WindowBg, string SidebarBg, string CardBg, string CardBorder, string TileBg,
        string TextPrimary, string TextSecondary, string TextTertiary,
        string Accent, string NavHover, string NavSelected, string Danger);

    static readonly Palette Dark = new(
        "#1C1C1C", "#181818", "#272727", "#33FFFFFF", "#2F2F2F",
        "#FFFFFF", "#C5C5C5", "#8E8E8E",
        "#60CDFF", "#14FFFFFF", "#1FFFFFFF", "#FF6B6B");

    static readonly Palette Light = new(
        "#F3F3F3", "#EAEAEA", "#FFFFFF", "#1A000000", "#EFEFEF",
        "#1A1A1A", "#4A4A4A", "#6E6E6E",
        "#005FB8", "#0F000000", "#1A000000", "#C42B1C");

    static bool _hooked;

    /// <summary>True when the effective theme (after resolving "System") is dark.</summary>
    public static bool IsDark { get; private set; } = true;

    public static event Action? Changed;

    /// <summary>Reads the Windows "apps use light theme" setting. Defaults to dark if unreadable.</summary>
    public static bool SystemIsDark()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return k?.GetValue("AppsUseLightTheme") is int v ? v == 0 : true;
        }
        catch { return true; }
    }

    public static void Apply()
    {
        var mode = AppState.Global.Theme;
        bool dark = mode switch
        {
            ThemeNames.Light => false,
            ThemeNames.Dark => true,
            _ => SystemIsDark(),
        };

        var app = Application.Current;
        if (app == null) return;

        IsDark = dark;
        app.ThemeMode = dark ? ThemeMode.Dark : ThemeMode.Light;
        Recolor(app.Resources, dark ? Dark : Light);
        TrayMenuTheme.SetDark(dark);

        if (!_hooked)
        {
            _hooked = true;
            // Follow live OS theme changes while the setting is "System".
            SystemEvents.UserPreferenceChanged += (_, e) =>
            {
                if (e.Category != UserPreferenceCategory.General) return;
                Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    if (AppState.Global.Theme == ThemeNames.System) Apply();
                });
            };
        }
        Changed?.Invoke();
    }

    static void Recolor(ResourceDictionary r, Palette p)
    {
        Set(r, "WindowBg", p.WindowBg);
        Set(r, "SidebarBg", p.SidebarBg);
        Set(r, "CardBg", p.CardBg);
        Set(r, "CardBorder", p.CardBorder);
        Set(r, "TileBg", p.TileBg);
        Set(r, "TextPrimary", p.TextPrimary);
        Set(r, "TextSecondary", p.TextSecondary);
        Set(r, "TextTertiary", p.TextTertiary);
        Set(r, "Accent", p.Accent);
        Set(r, "NavHover", p.NavHover);
        Set(r, "NavSelected", p.NavSelected);
        Set(r, "Danger", p.Danger);
    }

    static void Set(ResourceDictionary r, string key, string hex)
    {
        var color = (Color)ColorConverter.ConvertFromString(hex);
        if (r[key] is SolidColorBrush b && !b.IsFrozen) b.Color = color;
        else r[key] = new SolidColorBrush(color); // frozen/missing: replace (DynamicResource users update)
    }
}
