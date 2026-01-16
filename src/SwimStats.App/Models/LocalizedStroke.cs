using System.ComponentModel;
using SwimStats.Core.Models;
using SwimStats.App.Resources;

namespace SwimStats.App.Models;

public class LocalizedStroke : INotifyPropertyChanged
{
    private readonly Stroke _stroke;

    public LocalizedStroke(Stroke stroke)
    {
        _stroke = stroke;
        
        // Subscribe to localization changes
        LocalizationManager.Instance.PropertyChanged += (s, e) =>
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
        };
    }

    public Stroke Stroke => _stroke;

    public string DisplayName
    {
        get
        {
            var key = $"Stroke_{_stroke}";
            return LocalizationManager.Instance[key];
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public override bool Equals(object? obj)
    {
        if (obj is LocalizedStroke other)
            return _stroke == other._stroke;
        if (obj is Stroke stroke)
            return _stroke == stroke;
        return false;
    }

    public override int GetHashCode() => _stroke.GetHashCode();
}
