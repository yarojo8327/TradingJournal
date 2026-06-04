using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Application.WPF.Views.Converters;

/// <summary>Visible cuando el int es mayor que 0, Collapsed en caso contrario.</summary>
[ValueConversion(typeof(int), typeof(Visibility))]
public class IntToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int n && n > 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
