using System.Windows.Data;
using System.Windows.Markup;

namespace SwimStats.App.Resources;

[MarkupExtensionReturnType(typeof(BindingExpression))]
public class LocalizeExtension : MarkupExtension
{
    public string Key { get; set; }

    public LocalizeExtension(string key)
    {
        Key = key;
    }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding($"[{Key}]")
        {
            Source = LocalizationManager.Instance,
            Mode = BindingMode.OneWay
        };

        return binding.ProvideValue(serviceProvider);
    }
}
