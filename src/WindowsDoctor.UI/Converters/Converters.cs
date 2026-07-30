using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace WindowsDoctor.UI.Converters;

[ValueConversion(typeof(bool), typeof(bool))]
public class InverseBoolConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c) => v is bool b && !b;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => v is bool b && !b;
}

[ValueConversion(typeof(bool), typeof(Visibility))]
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c)
        => v is bool b && b ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
}

[ValueConversion(typeof(string), typeof(Visibility))]
public class NullOrEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c)
        => string.IsNullOrEmpty(v?.ToString()) ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
}

[ValueConversion(typeof(string), typeof(SolidColorBrush))]
public class StringToBrushConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c)
    {
        try
        {
            var hex = v?.ToString() ?? "#CDD6F4";
            var color = (Color)ColorConverter.ConvertFromString(hex);
            return new SolidColorBrush(color);
        }
        catch { return new SolidColorBrush(Colors.White); }
    }
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
}

[ValueConversion(typeof(double), typeof(SolidColorBrush))]
public class ScoreToColorBrushConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c)
    {
        var score = v is double d ? d : 0;
        var hex = score >= 80 ? "#A6E3A1" : score >= 60 ? "#F9E2AF" : score >= 40 ? "#FAB387" : "#F38BA8";
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
    }
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
}

[ValueConversion(typeof(int), typeof(Visibility))]
public class ZeroToCollapsedConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c)
        => v is int i && i > 0 ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
}
