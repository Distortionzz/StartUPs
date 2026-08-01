using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using StartUPs.Models;
using StartUPs.Services;

namespace StartUPs;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private const string AllCategoryId = "all";
    private const string UpdatesCategoryId = "updates";

    private readonly ObservableCollection<AppEntry> _apps = new();
    private ICollectionView _view = null!;
    private string _activeCategoryId = AllCategoryId;
    private string _searchText = "";

    private CancellationTokenSource? _cancelSource;
    private bool _isInstalling;

    public MainWindow()
    {
        InitializeComponent();
        LoadCatalog();
    }

    // ---------------------------------------------------------------- catalog

    private void LoadCatalog()
    {
        Catalog catalog;
        try
        {
            catalog = CatalogService.Load();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Could not load the app catalog",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        foreach (var app in catalog.Categories.SelectMany(c => c.Apps))
        {
            app.PropertyChanged += App_PropertyChanged;
            _apps.Add(app);
        }

        // Sidebar: a synthetic "All Apps" entry followed by the real categories.
        var sidebar = new List<Category>
        {
            new() { Id = AllCategoryId, Name = "All Apps", Icon = "\U0001F4E6" }
        };
        sidebar.AddRange(catalog.Categories);
        sidebar.Add(new Category { Id = UpdatesCategoryId, Name = "Updates", Icon = "⬇" });

        CategoryList.ItemsSource = sidebar;
        CategoryList.SelectedIndex = 0;

        _view = CollectionViewSource.GetDefaultView(_apps);
        _view.Filter = MatchesFilters;
        AppList.ItemsSource = _view;

        ApplyGrouping();
        UpdateSummary();
    }

    // ---------------------------------------------------------------- filtering

    private bool MatchesFilters(object item)
    {
        if (item is not AppEntry app) return false;

        if (_activeCategoryId != AllCategoryId && app.CategoryId != _activeCategoryId)
            return false;

        if (_searchText.Length == 0) return true;

        return app.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
            || app.Description.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
            || app.WingetId.Contains(_searchText, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Show category headings only when the list spans more than one category.</summary>
    private void ApplyGrouping()
    {
        _view.GroupDescriptions.Clear();
        if (_activeCategoryId == AllCategoryId)
            _view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(AppEntry.CategoryName)));
    }

    private void RefreshList()
    {
        if (_view is null) return;

        _view.Refresh();
        ApplyGrouping();

        bool anyVisible = _view.Cast<AppEntry>().Any();
        EmptyMessage.Visibility = anyVisible ? Visibility.Collapsed : Visibility.Visible;
    }

    // ---------------------------------------------------------------- events

    private void CategoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CategoryList.SelectedItem is not Category category) return;

        if (category.Id == UpdatesCategoryId)
        {
            ShowUpdatesView();
            return;
        }

        ShowAppsView();
        _activeCategoryId = category.Id;
        RefreshList();
    }

    // ---------------------------------------------------------------- view switching

    private void ShowUpdatesView()
    {
        UpdatePanel.Visibility = Visibility.Visible;
        AppScroller.Visibility = Visibility.Collapsed;
        EmptyMessage.Visibility = Visibility.Collapsed;
        SearchArea.Visibility = Visibility.Hidden;
        FooterActions.Visibility = Visibility.Collapsed;
        SelectionSummary.Text = $"StartUPs {UpdateService.CurrentVersion.ToString(3)}";
    }

    private void ShowAppsView()
    {
        UpdatePanel.Visibility = Visibility.Collapsed;
        AppScroller.Visibility = Visibility.Visible;
        SearchArea.Visibility = Visibility.Visible;
        FooterActions.Visibility = Visibility.Visible;
        UpdateSummary();
    }

    private void SearchInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        _searchText = SearchInput.Text.Trim();
        RefreshList();
    }

    private void App_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppEntry.IsSelected))
            UpdateSummary();
    }

    private void SelectEssentials_Click(object sender, RoutedEventArgs e)
    {
        foreach (var app in _apps)
            app.IsSelected = app.Essential;
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        foreach (var app in _apps)
            app.IsSelected = false;
    }

    // ---------------------------------------------------------------- install run

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        // While a run is active the same button acts as Cancel.
        if (_isInstalling)
        {
            _cancelSource?.Cancel();
            InstallButton.IsEnabled = false;
            InstallButton.Content = "Cancelling...";
            return;
        }

        var queue = _apps.Where(a => a.IsSelected).ToList();
        if (queue.Count == 0) return;

        if (!WingetService.IsAvailable())
        {
            MessageBox.Show(this,
                "winget (the Windows Package Manager) could not be found on this PC.\n\n" +
                "It ships with Windows 11 and recent Windows 10 builds as part of App Installer. " +
                "Install or update 'App Installer' from the Microsoft Store, then try again.",
                "winget not found", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        await RunInstallQueueAsync(queue);
    }

    private async Task RunInstallQueueAsync(List<AppEntry> queue)
    {
        _cancelSource = new CancellationTokenSource();
        var token = _cancelSource.Token;

        EnterInstallMode(queue);

        foreach (var app in queue)
            app.State = InstallState.Pending;

        int done = 0;

        try
        {
            foreach (var app in queue)
            {
                token.ThrowIfCancellationRequested();

                ProgressLabel.Text = $"{app.Name}  -  {done + 1} of {queue.Count}";

                // Skip anything already on the PC rather than reinstalling it.
                app.State = InstallState.Checking;
                if (await WingetService.IsInstalledAsync(app.WingetId, token))
                {
                    app.State = InstallState.AlreadyInstalled;
                }
                else
                {
                    app.State = InstallState.Installing;

                    // Progress<T> was created on the UI thread, so these callbacks
                    // marshal back to it automatically - safe to touch the card.
                    var reporter = new Progress<DownloadSample>(sample =>
                    {
                        app.ReportDownload(sample.Percent,
                            WingetService.FormatSpeed(sample.BytesPerSecond));

                        ProgressLabel.Text = sample.BytesPerSecond > 0
                            ? $"{app.Name}  -  {done + 1} of {queue.Count}   ({WingetService.FormatSpeed(sample.BytesPerSecond)})"
                            : $"{app.Name}  -  {done + 1} of {queue.Count}";
                    });

                    var result = await WingetService.InstallAsync(app.WingetId, reporter, token);

                    if (result.Succeeded)
                    {
                        app.State = InstallState.Installed;
                    }
                    else
                    {
                        app.LastError = $"winget exited with {result.ExitCodeHex}";
                        app.State = InstallState.Failed;
                    }
                }

                done++;
                InstallProgress.Value = done;
            }
        }
        catch (OperationCanceledException)
        {
            foreach (var app in queue)
            {
                if (app.State is InstallState.Pending or InstallState.Checking or InstallState.Installing)
                    app.State = InstallState.Cancelled;
            }
        }
        finally
        {
            ExitInstallMode();
            _cancelSource?.Dispose();
            _cancelSource = null;
        }

        ShowRunSummary(queue);
    }

    private void EnterInstallMode(List<AppEntry> queue)
    {
        _isInstalling = true;

        foreach (var app in _apps)
        {
            if (!queue.Contains(app))
                app.ResetState();
        }

        BodyGrid.IsEnabled = false;          // stop selection changing mid-run
        EssentialsButton.IsEnabled = false;
        ClearButton.IsEnabled = false;

        InstallButton.Content = "Cancel";
        InstallButton.IsEnabled = true;

        InstallProgress.Maximum = queue.Count;
        InstallProgress.Value = 0;
        ProgressArea.Visibility = Visibility.Visible;
        SelectionSummary.Text = $"Installing {queue.Count} app(s)...";
    }

    private void ExitInstallMode()
    {
        _isInstalling = false;

        BodyGrid.IsEnabled = true;
        EssentialsButton.IsEnabled = true;
        ClearButton.IsEnabled = true;
        ProgressArea.Visibility = Visibility.Collapsed;
        ProgressLabel.Text = "";

        UpdateSummary();
    }

    private void ShowRunSummary(List<AppEntry> queue)
    {
        int installed = queue.Count(a => a.State == InstallState.Installed);
        int already = queue.Count(a => a.State == InstallState.AlreadyInstalled);
        var failed = queue.Where(a => a.State == InstallState.Failed).ToList();
        int cancelled = queue.Count(a => a.State == InstallState.Cancelled);

        var message = new StringBuilder();
        message.AppendLine($"Installed:          {installed}");
        if (already > 0) message.AppendLine($"Already present:    {already}");
        if (cancelled > 0) message.AppendLine($"Cancelled:          {cancelled}");
        if (failed.Count > 0)
        {
            message.AppendLine($"Failed:             {failed.Count}");
            message.AppendLine();
            foreach (var app in failed)
                message.AppendLine($"  - {app.Name}: {app.LastError}");
        }

        MessageBox.Show(this, message.ToString(), "StartUPs - run complete",
            MessageBoxButton.OK,
            failed.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_isInstalling)
        {
            var answer = MessageBox.Show(this,
                "An install is still running. Closing now will interrupt it.\n\nClose anyway?",
                "StartUPs", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (answer == MessageBoxResult.No)
            {
                e.Cancel = true;
                return;
            }

            _cancelSource?.Cancel();
        }

        base.OnClosing(e);
    }

    // ---------------------------------------------------------------- updates

    private UpdateInfo? _pendingUpdate;
    private bool _updateBusy;

    private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (_updateBusy) return;
        _updateBusy = true;

        CheckUpdateButton.IsEnabled = false;
        CheckUpdateButton.Content = "Checking...";
        DownloadUpdateButton.Visibility = Visibility.Collapsed;
        ReleaseNotesHeader.Visibility = Visibility.Collapsed;
        ReleaseNotesCard.Visibility = Visibility.Collapsed;

        UpdateHeadline.Foreground = (Brush)FindResource("TextPrimary");
        UpdateHeadline.Text = "Checking GitHub...";
        UpdateDetail.Text = "";

        try
        {
            var info = await UpdateService.CheckAsync(CancellationToken.None);
            _pendingUpdate = info;
            ApplyUpdateResult(info);
        }
        finally
        {
            CheckUpdateButton.IsEnabled = true;
            CheckUpdateButton.Content = "Check again";
            _updateBusy = false;
        }
    }

    private void ApplyUpdateResult(UpdateInfo info)
    {
        string current = info.CurrentVersion.ToString(3);

        switch (info.Status)
        {
            case UpdateStatus.UpdateAvailable:
                // The green highlight is the whole point of this view.
                UpdateHeadline.Foreground = (Brush)FindResource("SuccessBrush");
                UpdateHeadline.Text = $"Update available  -  v{info.LatestVersion?.ToString(3)}";
                UpdateDetail.Text = info.CanDownload
                    ? $"You are running v{current}. The download is {UpdateService.FormatSize(info.DownloadSize)}. " +
                      "StartUPs will restart once it is installed."
                    : $"You are running v{current}, but this release has no downloadable StartUPs.exe attached.";

                DownloadUpdateButton.Visibility = info.CanDownload ? Visibility.Visible : Visibility.Collapsed;
                DownloadUpdateButton.IsEnabled = info.CanDownload;
                break;

            case UpdateStatus.UpToDate:
                UpdateHeadline.Foreground = (Brush)FindResource("TextPrimary");
                UpdateHeadline.Text = "You are up to date";
                UpdateDetail.Text = $"v{current} is the latest release.";
                break;

            default:
                UpdateHeadline.Foreground = (Brush)FindResource("FailureBrush");
                UpdateHeadline.Text = "Could not check for updates";
                UpdateDetail.Text = info.ErrorMessage;
                break;
        }

        if (!string.IsNullOrWhiteSpace(info.ReleaseNotes) && info.Status == UpdateStatus.UpdateAvailable)
        {
            ReleaseNotesText.Text = info.ReleaseNotes;
            ReleaseNotesHeader.Visibility = Visibility.Visible;
            ReleaseNotesCard.Visibility = Visibility.Visible;
        }
    }

    private async void DownloadUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (_updateBusy || _pendingUpdate?.DownloadUrl is null) return;

        var confirm = MessageBox.Show(this,
            $"Download v{_pendingUpdate.LatestVersion?.ToString(3)} and restart StartUPs?\n\n" +
            "The running version will be replaced.",
            "StartUPs", MessageBoxButton.OKCancel, MessageBoxImage.Question);

        if (confirm != MessageBoxResult.OK) return;

        _updateBusy = true;
        DownloadUpdateButton.IsEnabled = false;
        CheckUpdateButton.IsEnabled = false;
        UpdateProgress.Value = 0;
        UpdateProgress.Visibility = Visibility.Visible;

        var reporter = new Progress<(long Received, long Total)>(p =>
        {
            if (p.Total > 0) UpdateProgress.Value = p.Received * 100.0 / p.Total;
            UpdateDetail.Text =
                $"Downloading {UpdateService.FormatSize(p.Received)} of {UpdateService.FormatSize(p.Total)}...";
        });

        try
        {
            var file = await UpdateService.DownloadAsync(
                _pendingUpdate.DownloadUrl, _pendingUpdate.ExpectedSha256, reporter, CancellationToken.None);

            UpdateDetail.Text = "Checksum verified. Restarting to finish the update...";
            UpdateService.ApplyUpdateAndRestart(file);

            // The helper script waits for this process to exit before swapping the file.
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            UpdateProgress.Visibility = Visibility.Collapsed;
            UpdateHeadline.Foreground = (Brush)FindResource("FailureBrush");
            UpdateHeadline.Text = "Update failed";
            UpdateDetail.Text = ex.Message;
            DownloadUpdateButton.IsEnabled = true;
            CheckUpdateButton.IsEnabled = true;
        }
        finally
        {
            _updateBusy = false;
        }
    }

    // ---------------------------------------------------------------- summary

    private void UpdateSummary()
    {
        if (_isInstalling) return;

        int count = _apps.Count(a => a.IsSelected);

        SelectionSummary.Text = count switch
        {
            0 => $"No apps selected  -  {_apps.Count} available",
            1 => "1 app selected",
            _ => $"{count} apps selected"
        };

        InstallButton.IsEnabled = count > 0;
        InstallButton.Content = count > 0 ? $"Install Selected ({count})" : "Install Selected";
    }

    // ---------------------------------------------------------------- dark title bar

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        TryEnableDarkTitleBar();
    }

    private const int DwmwaUseImmersiveDarkMode = 20;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    /// <summary>Paints the Windows title bar dark so it matches the app. Ignored on older builds.</summary>
    private void TryEnableDarkTitleBar()
    {
        try
        {
            IntPtr handle = new WindowInteropHelper(this).Handle;
            int enabled = 1;
            DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref enabled, sizeof(int));
        }
        catch
        {
            // Not supported on this Windows version - harmless.
        }
    }
}
