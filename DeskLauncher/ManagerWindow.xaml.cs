using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace DeskLauncher;

public partial class ManagerWindow : Window
{
    static ManagerWindow? _instance;
    bool _loading;

    public static void ShowSingleton()
    {
        if (_instance == null)
        {
            _instance = new ManagerWindow();
            _instance.Closed += (_, _) => _instance = null;
            _instance.Show();
        }
        if (_instance.WindowState == WindowState.Minimized) _instance.WindowState = WindowState.Normal;
        _instance.Activate();
    }

    ManagerWindow()
    {
        InitializeComponent();
        AppState.Rebuild += LoadAll;
        AppState.FolderChanged += OnFolderChanged;
        Closed += (_, _) =>
        {
            AppState.Rebuild -= LoadAll;
            AppState.FolderChanged -= OnFolderChanged;
        };
        LoadAll();
    }

    FolderConfig? SelectedFolder => FolderList.SelectedItem as FolderConfig;

    void LoadAll()
    {
        _loading = true;
        ProfileList.ItemsSource = null;
        ProfileList.ItemsSource = AppState.Global.Profiles;
        var active = AppState.Global.Profiles.FirstOrDefault(p => p.Id == AppState.Global.ActiveProfileId);
        ProfileList.SelectedItem = active;
        ActiveProfileText.Text = active?.Name ?? "";
        AutoStartBox.IsChecked = AppState.Global.StartOnLogin;
        ThemeBox.SelectedItem = ThemeBox.Items.OfType<ComboBoxItem>()
            .FirstOrDefault(i => (string?)i.Tag == AppState.Global.Theme) ?? ThemeBox.Items[0];
        _loading = false;
        ReloadFolders(null);
    }

    void ReloadFolders(FolderConfig? select)
    {
        FolderList.ItemsSource = null;
        FolderList.ItemsSource = AppState.Profile.Folders;
        FolderList.SelectedItem = select ?? AppState.Profile.Folders.FirstOrDefault();
    }

    void OnFolderChanged(FolderConfig f)
    {
        if (!AppState.Profile.Folders.Contains(f)) return;
        var sel = SelectedFolder;
        FolderList.Items.Refresh(); // POCO rows: re-evaluate name, count, icon
        if (sel != null && FolderList.SelectedItem != sel) FolderList.SelectedItem = sel;
        if (f == sel) ShowFolder(f);
    }

    void UpdateLibraryCount()
    {
        int n = AppState.Icons.List().Count;
        LibraryCountText.Text = n == 1 ? "1 icono agregado" : $"{n} iconos agregados";
    }

    void OpenLibrary_Click(object sender, RoutedEventArgs e)
    {
        IconPickerWindow.Manage(this);
        UpdateLibraryCount();
    }

    void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (PageFolders == null) return; // fires during InitializeComponent
        PageFolders.Visibility = NavFolders.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PageProfiles.Visibility = NavProfiles.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PageSettings.Visibility = NavSettings.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        UpdateLibraryCount();
    }

    void ShowFolder(FolderConfig? f)
    {
        NameBox.Text = f?.Name ?? "";
        FolderIconPreview.Source = f != null ? AppState.Icons.GetFolder(f) : null;
        var wasLoading = _loading;
        _loading = true; // reflect the folder's style without writing it back
        PanelStyleBox.SelectedItem = PanelStyleBox.Items.OfType<ComboBoxItem>()
            .FirstOrDefault(i => (string?)i.Tag == (f?.PanelStyle ?? PanelStyles.Arc));
        PanelStyleBox.IsEnabled = f != null;
        _loading = wasLoading;
        var keep = AppList.SelectedItem is ShortcutItem sel ? sel.Id : null;
        AppList.ItemsSource = null;
        var list = f == null ? null
            : new ObservableCollection<ShortcutItem>(f.Shortcuts.OrderBy(s => s.SortOrder));
        AppList.ItemsSource = list;
        if (keep != null) AppList.SelectedItem = list?.FirstOrDefault(s => s.Id == keep);
    }

    void FolderIcon_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedFolder is not { } f) return;
        var name = IconPickerWindow.Pick(this, f.CustomIcon);
        if (name != null) AppState.SetFolderIcon(f, name);
    }

    void FolderIconReset_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedFolder is { } f) AppState.SetFolderIcon(f, null);
    }

    void AppIcon_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedFolder is not { } f || AppList.SelectedItem is not ShortcutItem s) return;
        var name = IconPickerWindow.Pick(this, s.CustomIcon);
        if (name != null) AppState.SetShortcutIcon(f, s, name);
    }

    void AppIconReset_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedFolder is { } f && AppList.SelectedItem is ShortcutItem s)
            AppState.SetShortcutIcon(f, s, null);
    }

    void Profile_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || ProfileList.SelectedItem is not Profile p) return;
        AppState.SwitchProfile(p.Id); // raises Rebuild -> LoadAll
    }

    void AddProfile_Click(object sender, RoutedEventArgs e)
    {
        var name = Prompt.Ask("Nombre del perfil", "Nuevo perfil", this);
        if (!string.IsNullOrWhiteSpace(name)) AppState.AddProfile(name.Trim());
    }

    void Folder_Changed(object sender, SelectionChangedEventArgs e)
    {
        ShowFolder(SelectedFolder);
        UpdateActionState();
    }

    void AppList_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateActionState();

    /// <summary>Selection-dependent actions: enabled (and tinted by style) only with a selection.</summary>
    void UpdateActionState()
    {
        DeleteFolderBtn.IsEnabled = SelectedFolder != null;
        bool app = AppList.SelectedItem is ShortcutItem;
        AppIconBtn.IsEnabled = app;
        AppResetBtn.IsEnabled = app;
    }

    void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        var f = AppState.AddFolder("Nueva carpeta");
        ReloadFolders(f);
    }

    void DeleteFolder_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedFolder is { } f) AppState.RemoveFolder(f);
    }

    void Name_LostFocus(object sender, RoutedEventArgs e)
    {
        if (SelectedFolder is not { } f) return;
        var n = NameBox.Text.Trim();
        if (n.Length == 0 || n == f.Name) return;
        f.Name = n;
        AppState.Renamed(f);
        ReloadFolders(f);
    }

    void AddApp_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedFolder is not { } f) return;
        var dlg = new OpenFileDialog
        {
            Title = "Elegir apps",
            Multiselect = true,
            Filter = "Apps y accesos directos|*.exe;*.lnk|Todos los archivos|*.*",
        };
        if (dlg.ShowDialog(this) != true) return;
        foreach (var p in dlg.FileNames)
            if (ShortcutResolver.FromPath(p) is { } item) AppState.AddShortcut(f, item);
    }

    void DeleteApp_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedFolder is { } f && (sender as FrameworkElement)?.DataContext is ShortcutItem s)
            AppState.RemoveShortcut(f, s);
    }

    // ---- responsive: icon-only buttons when the detail card is narrow ----

    const double CompactWidth = 720;
    bool _compact;

    void DetailCard_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.NewSize.Width <= 0) return; // page hidden
        bool compact = e.NewSize.Width < CompactWidth;
        if (compact == _compact) return;
        _compact = compact;
        Resources["LabelVis"] = compact ? Visibility.Collapsed : Visibility.Visible;
    }

    // ---- reorder apps: drag the grip; the list reorders live while dragging ----

    Point _dragStart;
    ShortcutItem? _dragItem;

    ShortcutItem? ItemAt(object source) =>
        source is DependencyObject d && ItemsControl.ContainerFromElement(AppList, d) is ListBoxItem c
            ? c.DataContext as ShortcutItem : null;

    static bool IsGrip(DependencyObject? d, DependencyObject stop)
    {
        for (; d != null && d != stop; d = d is Visual or System.Windows.Media.Media3D.Visual3D
                 ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
            if (d is FrameworkElement { Tag: "grip" }) return true;
        return false;
    }

    void AppList_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragItem = null;
        if (!IsGrip(e.OriginalSource as DependencyObject, AppList)) return;
        _dragStart = e.GetPosition(null);
        _dragItem = ItemAt(e.OriginalSource);
        if (_dragItem != null) AppList.SelectedItem = _dragItem;
    }

    void AppList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragItem == null || e.LeftButton != MouseButtonState.Pressed) return;
        var d = e.GetPosition(null) - _dragStart;
        if (Math.Abs(d.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(d.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        var item = _dragItem;
        _dragItem = null;
        var folder = SelectedFolder;
        var result = DragDrop.DoDragDrop(AppList, new DataObject(typeof(ShortcutItem), item), DragDropEffects.Move);

        if (folder == null) return;
        if (result == DragDropEffects.Move && AppList.ItemsSource is ObservableCollection<ShortcutItem> col)
            AppState.ReorderShortcuts(folder, col);
        else
            ShowFolder(folder); // cancelled (Esc): restore saved order
    }

    void AppList_DragOver(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (!e.Data.GetDataPresent(typeof(ShortcutItem)) ||
            AppList.ItemsSource is not ObservableCollection<ShortcutItem> col)
        {
            e.Effects = DragDropEffects.None;
            return;
        }
        e.Effects = DragDropEffects.Move;

        // Auto-scroll near the top/bottom edge.
        var pos = e.GetPosition(AppList);
        var sv = FindScrollViewer(AppList);
        if (sv != null)
        {
            if (pos.Y < 28) sv.ScrollToVerticalOffset(sv.VerticalOffset - 12);
            else if (pos.Y > AppList.ActualHeight - 28) sv.ScrollToVerticalOffset(sv.VerticalOffset + 12);
        }

        // Live reorder: the dragged row takes the slot of the row under the pointer.
        if (e.Data.GetData(typeof(ShortcutItem)) is not ShortcutItem moved) return;
        var target = ItemAt(e.OriginalSource);
        if (target == null || target == moved) return;
        int from = col.IndexOf(moved), to = col.IndexOf(target);
        if (from < 0 || to < 0) return;
        col.Move(from, to);
        AppList.SelectedItem = moved;
    }

    void AppList_Drop(object sender, DragEventArgs e) => e.Handled = true; // order already applied live

    static ScrollViewer? FindScrollViewer(DependencyObject d)
    {
        if (d is ScrollViewer sv) return sv;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(d); i++)
            if (FindScrollViewer(VisualTreeHelper.GetChild(d, i)) is { } r) return r;
        return null;
    }

    void PanelStyle_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || SelectedFolder is not { } f ||
            PanelStyleBox.SelectedItem is not ComboBoxItem { Tag: string style }) return;
        AppState.SetFolderStyle(f, style); // applies the next time the folder is opened
    }

    void Theme_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || ThemeBox.SelectedItem is not ComboBoxItem { Tag: string mode }) return;
        if (mode == AppState.Global.Theme) return;
        AppState.Global.Theme = mode;
        AppState.SaveGlobal();
        ThemeService.Apply();
    }

    void AutoStart_Click(object sender, RoutedEventArgs e)
    {
        AppState.Global.StartOnLogin = AutoStartBox.IsChecked == true;
        AppState.SaveGlobal();
        AutoStartService.Set(AppState.Global.StartOnLogin);
    }
}

/// <summary>Minimal text-input dialog.</summary>
public static class Prompt
{
    public static string? Ask(string title, string defaultValue, Window? owner)
    {
        var box = new TextBox { Text = defaultValue, Margin = new Thickness(0, 0, 0, 12), MinWidth = 280 };
        var ok = new Button { Content = "OK", IsDefault = true, Width = 70, Margin = new Thickness(0, 0, 6, 0) };
        var cancel = new Button { Content = "Cancelar", IsCancel = true, Width = 70 };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        var stack = new StackPanel { Margin = new Thickness(14) };
        var logo = new Image
        {
            Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Assets/logo.png")),
            Width = 128, Height = 128,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 0, 0, 8),
        };
        RenderOptions.SetBitmapScalingMode(logo, System.Windows.Media.BitmapScalingMode.HighQuality);
        stack.Children.Add(logo);
        stack.Children.Add(box);
        stack.Children.Add(buttons);

        var win = new Window
        {
            Title = title,
            Content = stack,
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStartupLocation = owner != null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            Owner = owner,
            FontFamily = (FontFamily)Application.Current.Resources["UiFont"],
            Background = (Brush)Application.Current.Resources["WindowBg"],
        };
        ok.Click += (_, _) => win.DialogResult = true;
        win.Loaded += (_, _) => { box.Focus(); box.SelectAll(); };
        return win.ShowDialog() == true ? box.Text : null;
    }
}
