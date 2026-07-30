using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WindowsDoctor.UI.Converters;

public class NavStyleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var current = value?.ToString() ?? "";
        var target  = parameter?.ToString() ?? "";
        var key = current.Equals(target, StringComparison.OrdinalIgnoreCase)
            ? "SidebarButtonActive"
            : "SidebarButton";
        return Application.Current.FindResource(key) as System.Windows.Style
               ?? Application.Current.FindResource("SidebarButton")!;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
