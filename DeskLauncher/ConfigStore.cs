using System.IO;
using System.Text.Json;

namespace DeskLauncher;

/// <summary>JSON persistence with atomic writes and .bak recovery.</summary>
public sealed class ConfigStore
{
    static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public string Root { get; }

    public ConfigStore(string root)
    {
        Root = root;
        Directory.CreateDirectory(root);
    }

    public string IconCacheDir => Path.Combine(Root, "iconcache");
    public string CustomIconDir => Path.Combine(Root, "icons");
    string GlobalPath => Path.Combine(Root, "global.json");
    string ProfilePath(string id) => Path.Combine(ProfileDir(id), "config.json");

    /// <summary>
    /// Profile ids come from global.json (user-writable), so they are never trusted as paths:
    /// only GUIDs are accepted, which rules out "..\" traversal before any read, write or delete.
    /// </summary>
    string ProfileDir(string id)
    {
        if (!IsValidProfileId(id)) throw new ArgumentException("Invalid profile id.", nameof(id));
        return Path.Combine(Root, "profiles", Guid.Parse(id).ToString());
    }

    public static bool IsValidProfileId(string? id) => Guid.TryParse(id, out _);

    public GlobalConfig LoadGlobal() => Load<GlobalConfig>(GlobalPath);
    public void SaveGlobal(GlobalConfig c) => Save(GlobalPath, c);

    public ProfileConfig LoadProfile(string id) => Load<ProfileConfig>(ProfilePath(id));
    public void SaveProfile(string id, ProfileConfig c) => Save(ProfilePath(id), c);

    public void DeleteProfile(string id)
    {
        try { Directory.Delete(ProfileDir(id), true); }
        catch (IOException) { }
    }

    public static T Load<T>(string path) where T : new()
    {
        foreach (var p in new[] { path, path + ".bak" })
        {
            try
            {
                if (!File.Exists(p)) continue;
                var v = JsonSerializer.Deserialize<T>(File.ReadAllText(p), Json);
                if (v != null) return v;
            }
            catch (JsonException) { /* fall through to .bak */ }
            catch (IOException) { }
        }
        return new T();
    }

    public static void Save<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(value, Json));
        if (File.Exists(path)) File.Replace(tmp, path, path + ".bak");
        else File.Move(tmp, path);
    }
}
