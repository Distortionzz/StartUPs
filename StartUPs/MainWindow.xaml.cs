using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;
using StartUPs.Models;
using StartUPs.Services;

namespace StartUPs;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private const string AllCategoryId = "all";

    private readonly ObservableCollection<AppEntry> _apps = new();
    private ICollectionView _view = null!;
    private string _activeCategoryId = AllCategoryId;
    private string _searchText = "";

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
        _activeCategoryId = category.Id;
        RefreshList();
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

    private void Install_Click(object sender, RoutedEventArgs e)
    {
        var selected = _apps.Where(a => a.IsSelected).ToList();
        if (selected.Count == 0) return;

        // Step 5 replaces this placeholder with the real winget install queue.
        var names = string.Join("\n", selected.Select(a => $"  - {a.Name}  ({a.WingetId})"));
        MessageBox.Show(this,
            $"Ready to install {selected.Count} app(s):\n\n{names}\n\n" +
            "The winget install engine gets wired up in the next step.",
            "StartUPs", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // ---------------------------------------------------------------- summary

    private void UpdateSummary()
    {
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
