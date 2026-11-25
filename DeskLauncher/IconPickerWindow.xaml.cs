using System.Windows;

namespace DeskLauncher;

/// <summary>Modal icon picker backed by the icon library (with add/remove).</summary>
public partial class IconPickerWindow : Window
{
    string? _picked;

    IconPickerWindow(string? current)
    {
        InitializeComponent();
        Library.Select(current);
        Library.IconActivated += name => { _picked = name; DialogResult = true; };
    }

    void Ok_Click(object sender, RoutedEventArgs e)
    {
        _picked = Library.SelectedIcon;
        if (_picked == null) return; // nothing chosen: stay open
        DialogResult = true;
    }

    /// <summary>Opens the library in manage mode (add/remove only, nothing to select).</summary>
    public static void Manage(Window? owner)
    {
        var w = new IconPickerWindow(null);
        w.Title = "Biblioteca de iconos";
        w.OkButton.Visibility = Visibility.Collapsed;
        w.CancelButton.Content = "Cerrar";
        w.Library.ActivationDisabled = true;
        if (owner != null && owner.IsVisible)
        {
            w.Owner = owner;
            w.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        w.ShowDialog();
    }

    /// <summary>Shows the picker. Returns the chosen icon name, or null if cancelled.</summary>
    public static string? Pick(Window? owner, string? current)
    {
        var w = new IconPickerWindow(current);
        if (owner != null && owner.IsVisible)
        {
            w.Owner = owner;
            w.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            w.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            w.Topmost = true; // opened from a desktop dock / popup: no owner window to sit above
        }
        return w.ShowDialog() == true ? w._picked : null;
    }
}
