using System.Diagnostics;
using System.Windows;

namespace StartUPs;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// Keep the splash up at least this long so it never flickers past. Sized to let
    /// all three loading messages land: 3 x 1.0s, plus the entrance animation.
    /// Deliberately short - the single-file exe already spends a few seconds
    /// self-extracting before any of this code runs.
    /// </summary>
    private static readonly TimeSpan MinimumSplashTime = TimeSpan.FromMilliseconds(3200);

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var splash = new SplashWindow();
        splash.Show();

        var clock = Stopwatch.StartNew();

        // Building the main window parses the catalog, so real work happens
        // behind the splash rather than the splash being pure decoration.
        var main = new MainWindow();
        MainWindow = main;

        var remaining = MinimumSplashTime - clock.Elapsed;
        if (remaining > TimeSpan.Zero)
            await Task.Delay(remaining);

        // Show the main window first: closing the last window would exit the app.
        main.Show();
        main.Activate();

        await splash.FadeOutAndCloseAsync();
    }
}
