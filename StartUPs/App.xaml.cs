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
    /// all five loading messages land: 5 x 1.1s of message time, plus the intro.
    /// </summary>
    private static readonly TimeSpan MinimumSplashTime = TimeSpan.FromMilliseconds(5600);

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
