using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DeskLauncher;

/// <summary>Icon library grid with add/remove. Hosted in Settings and in <see cref="IconPickerWindow"/>.</summary>
public partial class IconLibraryView : UserControl
{
    public sealed record Entry(string Name, ImageSource? Image);

    /// <summary>Raised when the user double-clicks an icon.</summary>
    public event Action<string>? IconActivated;

    /// <summary>When true, double-click does not raise <see cref="IconActivated"/> (manage-only mode).</summary>
    public bool ActivationDisabled { get; set; }

    public IconLibraryView()
    {
        InitializeComponent();
        Reload();
    }

    public string? SelectedIcon => (IconGrid.SelectedItem as Entry)?.Name;

    public void Select(string? name)
    {
        IconGrid.SelectedItem = IconGrid.Items.OfType<Entry>().FirstOrDefault(e => e.Name == name);
    }

    public void Reload()
    {
        var keep = SelectedIcon;
        var entries = AppState.Icons.List().Select(n => new Entry(n, AppState.Icons.GetByName(n))).ToList();
        IconGrid.ItemsSource = entries;
        EmptyText.Visibility = entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        CountText.Text = entries.Count == 1 ? "1 icono" : $"{entries.Count} iconos";
        Select(keep);
    }

    void Add_Click(object sender, RoutedEventArgs e)
    {
        var added = AppState.Icons.AddFromDialog(Window.GetWindow(this));
        if (added.Count == 0) return;
        Reload();
        Select(added[^1]);
        IconGrid.ScrollIntoView(IconGrid.SelectedItem);
    }

    void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedIcon is not { } name) return;
        var r = MessageBox.Show(Window.GetWindow(this),
            "¿Quitar este icono de la biblioteca?\nLas carpetas y apps que lo usan volverán a su icono por defecto.",
            "DeskLauncher", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (r != MessageBoxResult.Yes) return;
        AppState.RemoveLibraryIcon(name);
        Reload();
    }

    void Grid_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!ActivationDisabled && SelectedIcon is { } name && e.OriginalSource is DependencyObject d &&
            ItemsControl.ContainerFromElement(IconGrid, d) is ListBoxItem)
            IconActivated?.Invoke(name);
    }
}
