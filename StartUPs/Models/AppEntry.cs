using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace StartUPs.Models;

/// <summary>One installable app from catalog.json.</summary>
public class AppEntry : INotifyPropertyChanged
{
    // --- Loaded from catalog.json ---
    public string WingetId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public bool Essential { get; set; }

    // --- Filled in at load time, not stored in the catalog ---
    [JsonIgnore] public string CategoryId { get; set; } = "";
    [JsonIgnore] public string CategoryName { get; set; } = "";

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
        }
    }

    /// <summary>Details of a failure, shown in the end-of-run summary.</summary>
    [JsonIgnore] public string LastError { get; set; } = "";

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
