using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace DeskLauncher;

/// <summary>Startup splash: icon + app name, visible for about one second.</summary>
public partial class SplashWindow : Window
{
    public const int DurationMs = 1500;
    static bool _showing;

    /// <summary>Shows the splash for <see cref="DurationMs"/>, then runs <paramref name="then"/>. Ignored while one is already up.</summary>
    public static void Run(Action? then = null)
    {
        if (_showing) return;
        _showing = true;
        var w = new SplashWindow();
        w.Closed += (_, _) =>
        {
            _showing = false;
            then?.Invoke();
        };
        w.Show();
    }

    SplashWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)));
            var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(DurationMs - 300) };
            t.Tick += (_, _) =>
            {
                t.Stop();
                var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150));
                fade.Completed += (_, _) => Close();
                BeginAnimation(OpacityProperty, fade);
            };
            t.Start();
        };
    }
}
