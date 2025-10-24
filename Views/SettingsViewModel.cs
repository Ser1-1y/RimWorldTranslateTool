using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RimWorldModTranslate.Views;

public class SettingsViewModel : INotifyPropertyChanged
{
    private string? _selectedLanguage;
    private bool _autoSave;
    private string? _apicaseApiToken;

    public ObservableCollection<string> Languages { get; } = ["English", "French", "German", "Spanish", "Russian"];

    public string? SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (_selectedLanguage == value) 
                return;
            
            _selectedLanguage = value;
            OnPropertyChanged();
        }
    }

    public bool AutoSave
    {
        get => _autoSave;
        set
        {
            if (_autoSave == value) 
                return;
            _autoSave = value;
            OnPropertyChanged();
        }
    }

    public string? ApicaseApiToken
    {
        get => _apicaseApiToken;
        set
        {
            if (_apicaseApiToken == value) 
                return;
            _apicaseApiToken = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}