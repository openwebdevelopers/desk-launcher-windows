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
        Loc.Apply(AppState.Global.Language);
        ThemeService.Apply();
        Loc.Changed += OnLanguageChanged;
        AppState.Rebuild += BuildDocks;
        AppState.FolderChanged += OnFolderChanged;

        BuildDocks();
        BuildTray();
        AutoStartService.Set(AppState.Global.StartOnLogin);

        // DESKLAUNCHER_OPEN=<folder name>: open that folder's panel after startup (demos, screenshots).
        if (Environment.GetEnvironmentVariable("DESKLAUNCHER_OPEN") is { Length: > 0 } open)
        {
            var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            t.Tick += (_, _) =>
            {
                t.Stop();
                _docks.FirstOrDefault(d => d.Folder.Name == open)?.ShowPanel();
            };
            t.Start();
        }

    }

    TaskbarStripWindow? _strip;

    void OnFolderChanged(FolderConfig f)
    {
        // Desktop dock: add / remove / refresh according to the folder's locations.
        var dock = _docks.FirstOrDefault(d => d.Folder == f);
        if (f.ShowOnDesktop && dock == null)
        {
            var d = new DockWindow(f);
            _docks.Add(d);
            d.Show();
            if (DockWindow.TakeSnapDirty()) AppState.SaveProfile();
        }
        else if (!f.ShowOnDesktop && dock != null)
        {
            dock.Close();
            _docks.Remove(dock);
        }
        else dock?.Refresh();

        SyncStrip();
    }

    /// <summary>Creates, updates or removes the taskbar strip from the folders that show on the taskbar.</summary>
    void SyncStrip()
    {
        var folders = AppState.Profile.Folders.Where(x => x.ShowOnTaskbar).ToList();
        if (folders.Count == 0)
        {
            _strip?.Close();
            _strip = null;
        }
        else if (_strip == null)
        {
            _strip = new TaskbarStripWindow(folders);
            _strip.Show();
        }
        else _strip.Refresh(folders);
    }

    void BuildDocks()
    {
        foreach (var d in _docks) d.Close();
        _strip?.Close();
        _strip = null;
        _docks.Clear();
        DockWindow.ResetClaims();
        foreach (var f in AppState.Profile.Folders.Where(x => x.ShowOnDesktop))
        {
            var d = new DockWindow(f);
            _docks.Add(d);
            d.Show();
        }
        if (DockWindow.TakeSnapDirty()) AppState.SaveProfile(); // docks were aligned to the desktop grid
        SyncStrip();
    }

    void OnLanguageChanged()
    {
        if (_tray != null)
        {
            var old = _tray.ContextMenuStrip;
            _tray.ContextMenuStrip = BuildTrayMenu();
            old?.Dispose();
        }
        BuildDocks(); // dock / strip context menus are built per window
        Dispatcher.BeginInvoke(ManagerWindow.Reopen); // not inside the old window's own event
    }

    void BuildTray()
    {
        _tray = new System.Windows.Forms.NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Text = "DeskLauncher",
            ContextMenuStrip = BuildTrayMenu(),
            Visible = true,
        };
        _tray.DoubleClick += (_, _) => OpenFromTray();
    }

    System.Windows.Forms.ContextMenuStrip BuildTrayMenu()
    {
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add(Loc.T("OpenManager"), null, (_, _) => OpenFromTray());
        menu.Items.Add(Loc.T("NewFolder"), null, (_, _) => AppState.AddFolder(Loc.T("NewFolder")));

        var profiles = new System.Windows.Forms.ToolStripMenuItem(Loc.T("Profile"));
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
        menu.Items.Add(Loc.T("Exit"), null, (_, _) => Shutdown());

        TrayMenuTheme.Apply(menu);
        return menu;
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
        _strip?.Close();
        _strip = null;
        _mutex?.Dispose();
    }
}
