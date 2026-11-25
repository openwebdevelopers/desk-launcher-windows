using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using static DeskLauncher.NativeMethods;

namespace DeskLauncher;

/// <summary>
/// Single entry point for every icon in the app: shell-extracted icons (cached as PNG),
/// and user-chosen custom icons (imported into <c>icons\</c> as owned PNG copies).
/// </summary>
public sealed class IconManager
{
    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellItemImageFactory
    {
        [PreserveSig] int GetImage(SIZE size, int flags, out IntPtr hbm);
    }

    [StructLayout(LayoutKind.Sequential)]
    struct SIZE { public int cx, cy; }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    static extern void SHCreateItemFromParsingName(string path, IntPtr pbc, ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory ppv);

    const int SIIGBF_ICONONLY = 0x4;
    const int Size = 96;

    static readonly string[] ImageExts = { ".png", ".jpg", ".jpeg", ".bmp", ".ico", ".gif" };
    static readonly string[] ShellExts = { ".exe", ".dll", ".lnk" };

    readonly string _cacheDir;
    readonly string _customDir;
    readonly Dictionary<string, BitmapSource?> _mem = new();

    public IconManager(string cacheDir, string customDir)
    {
        _cacheDir = cacheDir;
        _customDir = customDir;
        Directory.CreateDirectory(cacheDir);
        Directory.CreateDirectory(customDir);
    }

    // ---- lookup ----

    /// <summary>App icon: custom -> .lnk icon -> target. Null if nothing resolves.</summary>
    public BitmapSource? GetApp(ShortcutItem item)
    {
        var custom = GetCustom(item.CustomIcon);
        if (custom != null) return custom;
        foreach (var p in new[] { item.IconPath, item.TargetPath })
            if (!string.IsNullOrEmpty(p) && (File.Exists(p) || Directory.Exists(p)))
                return GetShell(p!);
        return null;
    }

    /// <summary>Folder icon, or null to let the dock draw its default glyph.</summary>
    public BitmapSource? GetFolder(FolderConfig folder) => GetCustom(folder.CustomIcon);

    /// <summary>Library icon by file name (null if missing).</summary>
    public BitmapSource? GetByName(string? name) => GetCustom(name);

    /// <summary>All icons in the library, oldest first.</summary>
    public List<string> List()
    {
        try
        {
            return new DirectoryInfo(_customDir).GetFiles("*.png")
                .OrderBy(f => f.CreationTimeUtc).Select(f => f.Name).ToList();
        }
        catch { return new List<string>(); }
    }

    BitmapSource? GetCustom(string? name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        var key = "custom:" + name;
        if (_mem.TryGetValue(key, out var cached)) return cached;
        BitmapSource? img = null;
        try
        {
            var file = Path.Combine(_customDir, Path.GetFileName(name));
            if (File.Exists(file)) img = LoadPng(file);
        }
        catch { }
        _mem[key] = img;
        return img;
    }

    BitmapSource? GetShell(string path)
    {
        if (_mem.TryGetValue(path, out var cached)) return cached;
        var png = Path.Combine(_cacheDir, Hash(path) + ".png");
        BitmapSource? img = null;
        try
        {
            if (File.Exists(png)) img = LoadPng(png);
        }
        catch { /* regenerate */ }

        if (img == null)
        {
            img = ExtractShell(path);
            if (img != null)
            {
                try { SavePng(img, png); } catch { }
            }
        }
        _mem[path] = img;
        return img;
    }

    // ---- custom icon lifecycle ----

    /// <summary>File picker (multi-select) that adds the chosen files to the library. Returns the new icon names.</summary>
    public List<string> AddFromDialog(Window? owner)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Agregar iconos",
            Multiselect = true,
            Filter = "Iconos e imágenes|*.png;*.jpg;*.jpeg;*.bmp;*.ico;*.gif;*.exe;*.dll;*.lnk|Todos los archivos|*.*",
        };
        bool ok = owner != null ? dlg.ShowDialog(owner) == true : dlg.ShowDialog() == true;
        var added = new List<string>();
        if (!ok) return added;
        int failed = 0;
        foreach (var f in dlg.FileNames)
        {
            var name = Import(f);
            if (name != null) added.Add(name); else failed++;
        }
        if (failed > 0)
            MessageBox.Show($"{failed} archivo(s) no se pudieron usar como icono.", "DeskLauncher");
        return added;
    }

    /// <summary>Copies the source into the custom icon dir as a PNG. Returns its file name, or null on failure.</summary>
    public string? Import(string sourceFile)
    {
        try
        {
            if (!File.Exists(sourceFile)) return null;
            var ext = Path.GetExtension(sourceFile).ToLowerInvariant();
            BitmapSource? img = null;
            if (ImageExts.Contains(ext)) img = DecodeImage(sourceFile);
            else if (ShellExts.Contains(ext)) img = ExtractShell(sourceFile);
            if (img == null) return null;

            var name = Guid.NewGuid().ToString("N") + ".png";
            SavePng(img, Path.Combine(_customDir, name));
            return name;
        }
        catch { return null; }
    }

    /// <summary>Deletes a library icon file. Callers must clear references first (see AppState.RemoveLibraryIcon).</summary>
    public void Delete(string? name)
    {
        if (string.IsNullOrEmpty(name)) return;
        _mem.Remove("custom:" + name);
        try { File.Delete(Path.Combine(_customDir, Path.GetFileName(name))); } catch { }
    }

    // ---- internals ----

    static BitmapSource? DecodeImage(string file)
    {
        using var fs = File.OpenRead(file);
        var decoder = BitmapDecoder.Create(fs, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames.OrderByDescending(f => f.PixelWidth * f.PixelHeight).FirstOrDefault();
        if (frame == null) return null;

        BitmapSource src = frame;
        int max = Math.Max(src.PixelWidth, src.PixelHeight);
        if (max > Size)
        {
            double k = (double)Size / max;
            src = new TransformedBitmap(src, new ScaleTransform(k, k));
        }
        var result = new WriteableBitmap(src);
        result.Freeze();
        return result;
    }

    static BitmapSource? ExtractShell(string path)
    {
        var iid = typeof(IShellItemImageFactory).GUID;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out var f);
                int hr = f.GetImage(new SIZE { cx = Size, cy = Size }, SIIGBF_ICONONLY, out var hbm);
                if (hr == 0 && hbm != IntPtr.Zero)
                {
                    try
                    {
                        var bs = Imaging.CreateBitmapSourceFromHBitmap(hbm, IntPtr.Zero,
                            Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                        bs.Freeze();
                        return bs;
                    }
                    finally { DeleteObject(hbm); }
                }
            }
            catch (COMException) { }
            Thread.Sleep(120); // shell sometimes returns 0x8000000A on first try
        }
        return null;
    }

    static void SavePng(BitmapSource img, string file)
    {
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(img));
        using var fs = File.Create(file);
        enc.Save(fs);
    }

    static BitmapSource LoadPng(string file)
    {
        var bi = new BitmapImage();
        bi.BeginInit();
        bi.CacheOption = BitmapCacheOption.OnLoad;
        bi.UriSource = new Uri(file);
        bi.EndInit();
        bi.Freeze();
        return bi;
    }

    static string Hash(string s) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s.ToLowerInvariant())))[..32];
}

/// <summary>XAML converter: FolderConfig -> its custom icon (null when using the default glyph).</summary>
public sealed class FolderIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is FolderConfig f ? AppState.Icons.GetFolder(f) : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>XAML converter: ShortcutItem -> its resolved icon.</summary>
public sealed class ShortcutIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is ShortcutItem s ? AppState.Icons.GetApp(s) : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
