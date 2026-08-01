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

    private string _status = "";

    /// <summary>Per-app install progress text, shown on the card. Empty until Step 5 wires up winget.</summary>
    [JsonIgnore]
    public string Status
    {
        get => _status;
        set
        {
            if (_status == value) return;
            _status = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasStatus));
        }
    }

    [JsonIgnore]
    public bool HasStatus => !string.IsNullOrEmpty(_status);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
