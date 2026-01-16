using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace SwimStats.App.Resources;

public class LocalizationManager : INotifyPropertyChanged
{
    private static LocalizationManager? _instance;
    private readonly ResourceManager _resourceManager;
    private CultureInfo _currentCulture;

    public static LocalizationManager Instance => _instance ??= new LocalizationManager();

    public event PropertyChangedEventHandler? PropertyChanged;

    private LocalizationManager()
    {
        _resourceManager = new ResourceManager("SwimStats.App.Resources.Strings", typeof(LocalizationManager).Assembly);
        _currentCulture = CultureInfo.CurrentCulture;
    }

    public string this[string key]
    {
        get
        {
            try
            {
                return _resourceManager.GetString(key, _currentCulture) ?? key;
            }
            catch
            {
                return key;
            }
        }
    }

    public CultureInfo CurrentCulture
    {
        get => _currentCulture;
        set
        {
            if (_currentCulture != value)
            {
                _currentCulture = value;
                CultureInfo.CurrentCulture = value;
                CultureInfo.CurrentUICulture = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
            }
        }
    }

    public void SetLanguage(string cultureName)
    {
        CurrentCulture = new CultureInfo(cultureName);
    }
}
