namespace DeskLauncher;

public static class PanelStyles
{
    public const string Arc = "Arc";
    public const string Ring = "Ring";
    public const string Phone = "Phone";
}

public sealed class GlobalConfig
{
    public int SchemaVersion { get; set; } = 1;
    public bool StartOnLogin { get; set; }
    /// <summary>"System" (follow Windows), "Light" or "Dark".</summary>
    public string Theme { get; set; } = "System";
    public string ActiveProfileId { get; set; } = "";
    public List<Profile> Profiles { get; set; } = new();
}

public sealed class Profile
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "Default";
}

public sealed class ProfileSettings
{
    public int MaxGridColumns { get; set; } = 4;
    public bool CollapseOnMouseLeave { get; set; } = true;
}

public sealed class ProfileConfig
{
    public int ConfigVersion { get; set; } = 1;
    public ProfileSettings Settings { get; set; } = new();
    public List<FolderConfig> Folders { get; set; } = new();
}

public sealed class FolderConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "New folder";

    // Position stored relative to a monitor (physical px) so it survives layout changes.
    public string? MonitorDeviceName { get; set; }
    public int OffsetX { get; set; } = 120;
    public int OffsetY { get; set; } = 120;

    /// <summary>File name inside the custom icon dir; null = default glyph.</summary>
    public string? CustomIcon { get; set; }

    /// <summary>How this folder's apps are shown when opened: "Arc", "Ring" or "Phone". Null = Arc.</summary>
    public string? PanelStyle { get; set; }

    public List<ShortcutItem> Shortcuts { get; set; } = new();
}

public sealed class ShortcutItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public string TargetPath { get; set; } = "";
    public string? Arguments { get; set; }
    public string? IconPath { get; set; }
    /// <summary>User-chosen icon (file name inside the custom icon dir); overrides IconPath/target.</summary>
    public string? CustomIcon { get; set; }
    public string? AppUserModelId { get; set; }
    public int SortOrder { get; set; }
}
