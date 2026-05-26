using System.ComponentModel;
using System.Globalization;
using DeliveryApp.Driver.Services;

namespace DeliveryApp.Driver.Converters;

[ContentProperty(nameof(Key))]
public class LocExtension : IMarkupExtension<BindingBase>
{
    public string Key { get; set; } = string.Empty;

    public BindingBase ProvideValue(IServiceProvider serviceProvider)
    {
        return new Binding
        {
            Source = LocaleStrings.Instance,
            Path = $"[{Key}]",
            Mode = BindingMode.OneWay
        };
    }

    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider) => ProvideValue(serviceProvider);
}

public class LocaleStrings : INotifyPropertyChanged
{
    public static LocaleStrings Instance { get; } = new();

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

public class BoolToLayoutConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isFromMe)
            return isFromMe ? LayoutOptions.End : LayoutOptions.Start;
        return LayoutOptions.Start;
    }
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class ChatBubbleColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isFromMe)
            return isFromMe ? Color.FromArgb("#FF5722") : Color.FromArgb("#F5F5F5");
        return Color.FromArgb("#F5F5F5");
    }
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class BoolToTextColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isFromMe)
            return isFromMe ? Colors.White : Colors.Black;
        return Colors.Black;
    }
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class BoolToSubTextColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isFromMe)
            return isFromMe ? Color.FromArgb("#FFCCBC") : Color.FromArgb("#9E9E9E");
        return Color.FromArgb("#9E9E9E");
    }
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}
