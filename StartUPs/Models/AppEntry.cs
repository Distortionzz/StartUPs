using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace StartUPs.Models;

/// <summary>One installable app from catalog.json.</summary>
public class AppEntry : INotifyPropertyChanged
{
    // --- Loaded from catalog.json ---
    public string WingetId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public bool Essential { get; set; }

    /// <summary>
    /// True when this package's installer accepts a target directory. Roughly two
    /// thirds of the catalog do; plain .exe and MSIX packages hardcode their own
    /// location and winget fails outright if --location is passed to them.
    /// </summary>
    public bool SupportsLocation { get; set; }

    /// <summary>
    /// The MSI property to set instead of using winget's --location, for packages
    /// that anchor their install folder somewhere --location cannot reach.
    ///
    /// winget's --location sets TARGETDIR. An MSI whose app folder hangs off
    /// ProgramFiles64Folder ignores that completely, because that is a system
    /// property resolved by Windows. Such packages usually still expose their own
    /// property - INSTALLDIR by convention - which can be set directly. Empty for
    /// everything else, which uses --location as normal.
    /// </summary>
    public string LocationProperty { get; set; } = "";

    // --- Filled in at load time, not stored in the catalog ---
    [JsonIgnore] public string CategoryId { get; set; } = "";
    [JsonIgnore] public string CategoryName { get; set; } = "";

    /// <summary>The app's brand glyph, or null when we don't have one bundled.</summary>
    [JsonIgnore] public Geometry? Glyph { get; set; }

    /// <summary>Raster icon, used for apps with no vector glyph.</summary>
    [JsonIgnore] public ImageSource? Bitmap { get; set; }

    [JsonIgnore] public bool HasBitmap => Bitmap is not null;

    /// <summary>Vector glyph is used only when there is no bitmap.</summary>
    [JsonIgnore] public bool HasGlyph => Bitmap is null && Glyph is not null;

    /// <summary>True when neither an icon nor a glyph exists - should never happen.</summary>
    [JsonIgnore] public bool HasNoIcon => Bitmap is null && Glyph is null;

    /// <summary>Brand colour where known, otherwise a deterministic palette colour.</summary>
    [JsonIgnore] public Brush AccentBrush { get; set; } = Brushes.Gray;

    /// <summary>The letter drawn when there is no brand glyph.</summary>
    [JsonIgnore]
    public string Initial => string.IsNullOrEmpty(Name)
        ? "?"
        : Name[..1].ToUpperInvariant();

    private bool _isInstalled;

    /// <summary>
    /// True when winget reports this package as present on the PC. Detected in
    /// bulk at startup, so it reflects only what winget can see - an app put on
    /// the machine by other means may not show up here.
    /// </summary>
    [JsonIgnore]
    public bool IsInstalled
    {
        get => _isInstalled;
        set
        {
            if (_isInstalled == value) return;
            _isInstalled = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowInstalledBadge));
        }
    }

    /// <summary>The version winget reports, when it knows one.</summary>
    [JsonIgnore] public string InstalledVersion { get; set; } = "";

    /// <summary>The badge would only compete with the live status during a run.</summary>
    [JsonIgnore] public bool ShowInstalledBadge => _isInstalled && _state == InstallState.None;

    private bool _isSelected;

    /// <summary>True when the user has ticked this app's checkbox.</summary>
    [JsonIgnore]
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            OnPropertyChanged();
        }
    }

    private InstallState _state = InstallState.None;

    /// <summary>Where this app sits in the current install run.</summary>
    [JsonIgnore]
    public InstallState State
    {
        get => _state;
        set
        {
            if (_state == value) return;
            _state = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(HasStatus));
            OnPropertyChanged(nameof(ShowProgressBar));
            OnPropertyChanged(nameof(ShowInstalledBadge));
        }
    }

    /// <summary>Details of a failure, shown in the end-of-run summary.</summary>
    [JsonIgnore] public string LastError { get; set; } = "";

    /// <summary>
    /// The folder this app was asked to install into, when a custom root was set.
    /// Checked after the run: winget reports success whether or not the installer
    /// actually honoured it, so the folder existing is the only real evidence.
    /// </summary>
    [JsonIgnore] public string RequestedLocation { get; set; } = "";

    private double _downloadPercent;
    private string _speedText = "";

    /// <summary>How full the green download bar is, 0-100.</summary>
    [JsonIgnore] public double DownloadPercent => _downloadPercent;

    /// <summary>Show the download bar only while bytes are actually moving.</summary>
    [JsonIgnore]
    public bool ShowProgressBar => _state == InstallState.Installing && _downloadPercent > 0;

    /// <summary>Called from the install queue as winget reports bytes transferred.</summary>
    public void ReportDownload(double percent, string speedText)
    {
        _downloadPercent = percent;
        _speedText = speedText;

        OnPropertyChanged(nameof(DownloadPercent));
        OnPropertyChanged(nameof(ShowProgressBar));
        OnPropertyChanged(nameof(StatusText));
    }

    /// <summary>The label shown on the right-hand side of the card.</summary>
    [JsonIgnore]
    public string StatusText => _state switch
    {
        InstallState.Pending => "Queued",
        InstallState.Checking => "Checking...",
        InstallState.Installing => DownloadingLabel(),
        InstallState.Installed => "Installed",
        InstallState.AlreadyInstalled => "Already installed",
        InstallState.Failed => "Failed",
        InstallState.Cancelled => "Cancelled",
        InstallState.Uninstalling => "Removing...",
        InstallState.Uninstalled => "Removed",
        _ => ""
    };

    /// <summary>While bytes flow show "45%  12.4 MB/s"; once downloaded, the installer is running.</summary>
    private string DownloadingLabel()
    {
        if (_downloadPercent <= 0) return "Starting...";
        if (_downloadPercent >= 100 || _speedText.Length == 0) return "Installing...";
        return $"{_downloadPercent:0}%   {_speedText}";
    }

    [JsonIgnore]
    public bool HasStatus => _state != InstallState.None;

    /// <summary>Clears any status left over from a previous run.</summary>
    public void ResetState()
    {
        LastError = "";
        _downloadPercent = 0;
        _speedText = "";
        State = InstallState.None;

        OnPropertyChanged(nameof(DownloadPercent));
        OnPropertyChanged(nameof(ShowProgressBar));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
