using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using RimWorldModTranslate.Services;

namespace RimWorldModTranslate.Views;

public class SettingsViewModel : INotifyPropertyChanged
{
    private string? _selectedLanguage;
    private bool _autoSave;
    private string? _apicaseApiToken;
    private TranslationProvider _selectedProvider;
    private bool _enableTranslation;
    private string? _googleApiKey;
    private string? _deeplApiKey;
    private string? _yandexApiKey;

    public ObservableCollection<string> Languages { get; } = [
        "English", "French", "German", "Spanish", "Russian", "Chinese", "Japanese", "Korean",
        "Portuguese", "Italian", "Dutch", "Polish", "Czech", "Hungarian", "Romanian", "Bulgarian",
        "Croatian", "Slovak", "Slovenian", "Estonian", "Latvian", "Lithuanian", "Finnish", "Swedish",
        "Norwegian", "Danish", "Ukrainian", "Belarusian", "Turkish", "Arabic", "Hebrew", "Hindi",
        "Thai", "Vietnamese", "Indonesian", "Malay", "Tagalog"
    ];

    public ObservableCollection<TranslationProvider> Providers { get; } = new(UnifiedTranslationService.GetAvailableProviders());

    public string? SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (_selectedLanguage == value)
            {
                return;
            }
            
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
            {
                return;
            }
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
            {
                return;
            }
            _apicaseApiToken = value;
            OnPropertyChanged();
        }
    }

    public TranslationProvider SelectedProvider
    {
        get => _selectedProvider;
        set
        {
            if (_selectedProvider == value)
            {
                return;
            }
            _selectedProvider = value;
            OnPropertyChanged();
        }
    }

    public bool EnableTranslation
    {
        get => _enableTranslation;
        set
        {
            if (_enableTranslation == value)
            {
                return;
            }
            _enableTranslation = value;
            OnPropertyChanged();
        }
    }

    public string? GoogleApiKey
    {
        get => _googleApiKey;
        set
        {
            if (_googleApiKey == value)
            {
                return;
            }
            _googleApiKey = value;
            OnPropertyChanged();
        }
    }

    public string? DeeplApiKey
    {
        get => _deeplApiKey;
        set
        {
            if (_deeplApiKey == value)
            {
                return;
            }
            _deeplApiKey = value;
            OnPropertyChanged();
        }
    }

    public string? YandexApiKey
    {
        get => _yandexApiKey;
        set
        {
            if (_yandexApiKey == value)
            {
                return;
            }
            _yandexApiKey = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}