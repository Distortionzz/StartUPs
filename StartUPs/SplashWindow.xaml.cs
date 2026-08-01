using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace StartUPs;

/// <summary>
/// Borderless animated splash shown while the app starts up.
/// </summary>
public partial class SplashWindow : Window
{
    /// <summary>How long each loading message stays on screen.</summary>
    private static readonly TimeSpan MessageInterval = TimeSpan.FromMilliseconds(1000);

    /// <summary>Shuffled each launch so the splash never reads identically twice.</summary>
    private static readonly string[] MessagePool =
    {
        "Loading the app catalog...",
        "Checking winget...",
        "Rounding up 48 apps...",
        "Sorting apps into categories...",
        "Warming up the install queue...",
        "Verifying package sources...",
        "Polishing the interface...",
        "Tidying up the shelves..."
    };

    /// <summary>Always shown last, so the sequence lands somewhere deliberate.</summary>
    private const string FinalMessage = "Almost there...";

    private readonly Queue<string> _messages = new();
    private readonly DispatcherTimer _timer = new();

    public SplashWindow()
    {
        InitializeComponent();

        // Read the version from the assembly so it can never fall out of step
        // with the build the way a hardcoded string does.
        VersionText.Text = $"v{Services.UpdateService.CurrentVersion.ToString(3)}";

        BuildMessageQueue();

        _timer.Interval = MessageInterval;
        _timer.Tick += (_, _) => AdvanceMessage();

        Loaded += OnLoaded;
        Closed += (_, _) => _timer.Stop();
    }

    private void BuildMessageQueue()
    {
        var shuffled = MessagePool.OrderBy(_ => Random.Shared.Next()).Take(2);

        foreach (var message in shuffled)
            _messages.Enqueue(message);

        _messages.Enqueue(FinalMessage);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AdvanceMessage();
        _timer.Start();
    }

    /// <summary>Cross-fades to the next message, holding on the last one.</summary>
    private void AdvanceMessage()
    {
        if (_messages.Count == 0)
        {
            _timer.Stop();
            return;
        }

        // Nothing showing yet - fade the first message straight in.
        if (LoadingMessage.Text.Length == 0)
        {
            LoadingMessage.Text = _messages.Dequeue();
            FadeMessageTo(1, 260);
            return;
        }

        var fadeOut = new DoubleAnimation
        {
            To = 0,
            Duration = TimeSpan.FromMilliseconds(150),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };

        fadeOut.Completed += (_, _) =>
        {
            if (_messages.Count == 0) return;
            LoadingMessage.Text = _messages.Dequeue();
            FadeMessageTo(1, 260);
        };

        LoadingMessage.BeginAnimation(OpacityProperty, fadeOut);
    }

    private void FadeMessageTo(double target, int milliseconds)
    {
        LoadingMessage.BeginAnimation(OpacityProperty, new DoubleAnimation
        {
            To = target,
            Duration = TimeSpan.FromMilliseconds(milliseconds),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
    }

    /// <summary>Fades the card out, then closes it. Awaitable so startup can sequence cleanly.</summary>
    public Task FadeOutAndCloseAsync()
    {
        _timer.Stop();

        var completion = new TaskCompletionSource();

        var fade = new DoubleAnimation
        {
            To = 0,
            Duration = TimeSpan.FromMilliseconds(280),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };

        fade.Completed += (_, _) =>
        {
            Close();
            completion.TrySetResult();
        };

        BeginAnimation(OpacityProperty, fade);
        return completion.Task;
    }
}
