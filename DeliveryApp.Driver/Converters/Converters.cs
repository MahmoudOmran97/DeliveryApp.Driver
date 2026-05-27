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

// int > 0 → true
public class IntToBoolConverter : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c)
        => v is int i && i > 0;

    public object ConvertBack(object? v, Type t, object? p, CultureInfo c)
        => throw new NotImplementedException();
}

// bool true → Green bg, false → Red bg
public class IsOpenToColorConverter : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c)
        => v is true ? Color.FromArgb("#E8F5E9") : Color.FromArgb("#FFEBEE");

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

// null or empty string → false, any text → true
// Pass ConverterParameter="invert" to flip
public class NullOrEmptyToBoolConverter : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c)
    {
        bool hasValue = !string.IsNullOrEmpty(v as string);
        bool invert = p is string s && s == "invert";
        return invert ? !hasValue : hasValue;
    }

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

// ✅ FIX: bool true → Green (online), false → Gray (offline)
public class BoolToOnlineColorConverter : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c)
        => v is true ? Color.FromArgb("#4CAF50") : Color.FromArgb("#9E9E9E");

    public object ConvertBack(object? v, Type t, object? p, CultureInfo c)
        => throw new NotImplementedException();
}