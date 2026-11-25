using System.Windows;

namespace DeskLauncher;

public partial class App : Application
{
    Mutex? _mutex;
    readonly List<DockWindow> _docks = new();
    System.Windows.Forms.NotifyIcon? _tray;

    void OnStartup(object sender, StartupEventArgs e)
    {
        _mutex = new Mutex(true, "DeskLauncher.SingleInstance", out bool first);
        if (!first)
        {
            Shutdown();
            return;
        }

        // Every window the app creates gets the app icon.
        var icon = new System.Windows.Media.Imaging.BitmapImage(
            new Uri("pack://application:,,,/Assets/appicon.png"));
        icon.Freeze();
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((s, _) => { if (s is Window w && w.Icon == null) w.Icon = icon; }));

        // --manager: open the Manager when the startup splash ends.
        SplashWindow.Run(e.Args.Contains("--manager") ? ManagerWindow.ShowSingleton : null);
        AppState.Init();
        ThemeService.Apply();
        AppState.Rebuild += BuildDocks;
        AppState.FolderChanged += f => _docks.FirstOrDefault(d => d.Folder == f)?.Refresh();

        BuildDocks();
        BuildTray();
        AutoStartService.Set(AppState.Global.StartOnLogin);

    }

    void BuildDocks()
    {
        foreach (var d in _docks) d.Close();
        _docks.Clear();
        foreach (var f in AppState.Profile.Folders)
        {
            var d = new DockWindow(f);
            _docks.Add(d);
            d.Show();
        }
    }

    void BuildTray()
    {
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Abrir Manager", null, (_, _) => OpenFromTray());
        menu.Items.Add("Nueva carpeta", null, (_, _) => AppState.AddFolder("Nueva carpeta"));

        var profiles = new System.Windows.Forms.ToolStripMenuItem("Perfil");
        profiles.DropDownOpening += (_, _) =>
        {
            profiles.DropDownItems.Clear();
            foreach (var p in AppState.Global.Profiles)
            {
                var id = p.Id;
                var item = new System.Windows.Forms.ToolStripMenuItem(p.Name)
                { Checked = id == AppState.Global.ActiveProfileId };
                item.Click += (_, _) => AppState.SwitchProfile(id);
                profiles.DropDownItems.Add(item);
            }
        };
        profiles.DropDownItems.Add("...");
        menu.Items.Add(profiles);

        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Salir", null, (_, _) => Shutdown());

        TrayMenuTheme.Apply(menu);

        _tray = new System.Windows.Forms.NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Text = "DeskLauncher",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _tray.DoubleClick += (_, _) => OpenFromTray();
    }

    /// <summary>Opening the app from the tray always shows the splash first.</summary>
    static void OpenFromTray() => SplashWindow.Run(ManagerWindow.ShowSingleton);

    static System.Drawing.Icon LoadTrayIcon()
    {
        try
        {
            var res = GetResourceStream(new Uri("pack://application:,,,/Assets/appicon.ico"));
            if (res != null) return new System.Drawing.Icon(res.Stream);
        }
        catch { }
        return System.Drawing.SystemIcons.Application;
    }

    void OnExit(object sender, ExitEventArgs e)
    {
        if (_tray != null)
        {
            _tray.Visible = false;
            _tray.Dispose();
        }
        foreach (var d in _docks) d.Close();
        _mutex?.Dispose();
    }
}
