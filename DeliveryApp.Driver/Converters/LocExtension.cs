using System.ComponentModel;
using DeliveryApp.Driver.Services;

namespace DeliveryApp.Driver.Converters;

[ContentProperty(nameof(Key))]
public class LocExtension : IMarkupExtension<string>
{
    public string Key { get; set; } = string.Empty;

    public string ProvideValue(IServiceProvider serviceProvider)
        => LocalizationService.Get(Key);

    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider)
        => ProvideValue(serviceProvider);
}

public class LocaleStrings : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string this[string key] => LocalizationService.Get(key);

    public void Refresh() =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));

    public string Tab_Home => LocalizationService.Get(nameof(Tab_Home));
    public string Tab_Orders => LocalizationService.Get(nameof(Tab_Orders));
    public string Tab_Earnings => LocalizationService.Get("Tab_Earnings");
    public string Tab_Profile => LocalizationService.Get(nameof(Tab_Profile));
    public string Login => LocalizationService.Get(nameof(Login));
    public string Logout => LocalizationService.Get(nameof(Logout));
    public FlowDirection Flow => LocalizationService.Flow;
}
