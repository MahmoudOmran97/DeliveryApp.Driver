using System.Globalization;

namespace DeliveryApp.Driver.Converters;

// true → false, false → true
public class InvertedBoolConverter : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c)
        => v is bool b && !b;
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c)
        => v is bool b && !b;
}

// bool true → Online green, false → Offline gray
public class BoolToOnlineColorConverter : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c)
        => v is true ? Color.FromArgb("#4CAF50") : Color.FromArgb("#9E9E9E");
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c)
        => throw new NotImplementedException();
}

// null or empty string → false, has value → true
public class StringNotEmptyConverter : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c)
        => !string.IsNullOrEmpty(v as string);
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c)
        => throw new NotImplementedException();
}

// bool true → Primary orange, false → gray border
public class BoolToColorConverter : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c)
        => v is true ? Color.FromArgb("#FF5722") : Color.FromArgb("#E0E0E0");
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c)
        => throw new NotImplementedException();
}

// unread notification → light orange bg
public class IsReadToColorConverter : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c)
        => v is true ? Colors.White : Color.FromArgb("#FFF3EF");
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c)
        => throw new NotImplementedException();
}
