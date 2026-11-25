using System.IO;

namespace DeskLauncher;

public static class AppState
{
    public static ConfigStore Store = null!;
    public static GlobalConfig Global = null!;
    public static ProfileConfig Profile = null!;
    public static IconManager Icons = null!;

    /// <summary>Raised when docks must be rebuilt (profile switch, folder added/removed).</summary>
    public static event Action? Rebuild;
    /// <summary>Raised when a folder's content changed (shortcuts, name).</summary>
    public static event Action<FolderConfig>? FolderChanged;

    public static void Init()
    {
        // DESKLAUNCHER_DATA overrides the data folder (demos, screenshots, tests).
        var root = Environment.GetEnvironmentVariable("DESKLAUNCHER_DATA")
                   ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DeskLauncher");
        Store = new ConfigStore(root);
        Icons = new IconManager(Store.IconCacheDir, Store.CustomIconDir);
        Global = Store.LoadGlobal();

        if (Global.Profiles.Count == 0)
            Global.Profiles.Add(new Profile { Name = "Default" });
        if (Global.Profiles.All(p => p.Id != Global.ActiveProfileId))
            Global.ActiveProfileId = Global.Profiles[0].Id;
        Store.SaveGlobal(Global);

        LoadActive();
    }

    static void LoadActive()
    {
        Profile = Store.LoadProfile(Global.ActiveProfileId);
        if (Profile.Folders.Count == 0)
        {
            Profile.Folders.Add(new FolderConfig { Name = "Apps" });
            SaveProfile();
        }
    }

    public static void SaveProfile() => Store.SaveProfile(Global.ActiveProfileId, Profile);
    public static void SaveGlobal() => Store.SaveGlobal(Global);

    public static void SwitchProfile(string id)
    {
        if (id == Global.ActiveProfileId || Global.Profiles.All(p => p.Id != id)) return;
        Global.ActiveProfileId = id;
        SaveGlobal();
        LoadActive();
        Rebuild?.Invoke();
    }

    public static void AddProfile(string name)
    {
        var p = new Profile { Name = name };
        Global.Profiles.Add(p);
        SaveGlobal();
        SwitchProfile(p.Id);
    }

    public static FolderConfig AddFolder(string name)
    {
        // Stagger new folders so they do not stack.
        int n = Profile.Folders.Count;
        var f = new FolderConfig
        {
            Name = name,
            MonitorDeviceName = System.Windows.Forms.Screen.PrimaryScreen?.DeviceName,
            OffsetX = 120 + 90 * (n % 8),
            OffsetY = 120 + 110 * (n / 8),
        };
        Profile.Folders.Add(f);
        SaveProfile();
        Rebuild?.Invoke();
        return f;
    }

    public static void RemoveFolder(FolderConfig f)
    {
        Profile.Folders.Remove(f);
        SaveProfile();
        Rebuild?.Invoke();
    }

    public static void AddShortcut(FolderConfig f, ShortcutItem s)
    {
        s.SortOrder = f.Shortcuts.Count == 0 ? 0 : f.Shortcuts.Max(x => x.SortOrder) + 1;
        f.Shortcuts.Add(s);
        SaveProfile();
        FolderChanged?.Invoke(f);
    }

    public static void RemoveShortcut(FolderConfig f, ShortcutItem s)
    {
        f.Shortcuts.Remove(s);
        SaveProfile();
        FolderChanged?.Invoke(f);
    }

    /// <summary>Persists a new app order (SortOrder = position in <paramref name="ordered"/>).</summary>
    public static void ReorderShortcuts(FolderConfig f, IList<ShortcutItem> ordered)
    {
        for (int i = 0; i < ordered.Count; i++) ordered[i].SortOrder = i;
        SaveProfile();
        FolderChanged?.Invoke(f);
    }

    /// <summary>Sets how a folder shows its apps ("Arc", "Ring", "Phone"). Applies on next open.</summary>
    public static void SetFolderStyle(FolderConfig f, string style)
    {
        if ((f.PanelStyle ?? PanelStyles.Arc) == style) return;
        f.PanelStyle = style;
        SaveProfile();
        FolderChanged?.Invoke(f);
    }

    /// <summary>Sets (or clears with null) a folder's custom icon, library keeps the file.</summary>
    public static void SetFolderIcon(FolderConfig f, string? icon)
    {
        if (f.CustomIcon == icon) return;
        f.CustomIcon = icon;
        SaveProfile();
        FolderChanged?.Invoke(f);
    }

    /// <summary>Sets (or clears with null) an app's custom icon, library keeps the file.</summary>
    public static void SetShortcutIcon(FolderConfig f, ShortcutItem s, string? icon)
    {
        if (s.CustomIcon == icon) return;
        s.CustomIcon = icon;
        SaveProfile();
        FolderChanged?.Invoke(f);
    }

    /// <summary>
    /// Removes an icon from the library: clears every reference in all profiles (folders and apps
    /// fall back to their default icon), then deletes the file.
    /// </summary>
    public static void RemoveLibraryIcon(string name)
    {
        foreach (var p in Global.Profiles)
        {
            bool active = p.Id == Global.ActiveProfileId;
            var cfg = active ? Profile : Store.LoadProfile(p.Id);
            bool changed = false;
            foreach (var f in cfg.Folders)
            {
                if (f.CustomIcon == name) { f.CustomIcon = null; changed = true; }
                foreach (var s in f.Shortcuts)
                    if (s.CustomIcon == name) { s.CustomIcon = null; changed = true; }
            }
            if (changed) Store.SaveProfile(p.Id, cfg);
        }
        Icons.Delete(name);
        Rebuild?.Invoke();
    }

    public static void Renamed(FolderConfig f)
    {
        SaveProfile();
        FolderChanged?.Invoke(f);
    }
}
