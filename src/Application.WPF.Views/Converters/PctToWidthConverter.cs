using System.Globalization;
using System.Windows.Data;

namespace Application.WPF.Views.Converters;

/// <summary>
/// MultiValueConverter: [0] double pct (0–1 or 0–100), [1] double availableWidth → pixel width.
/// Supports both fractional (≤1) and percentage (>1) pct values.
/// </summary>
public sealed class PctToWidthConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2) return 0d;

        double pct   = values[0] is double d ? d : 0d;
        double width = values[1] is double w ? w : 0d;

        // Normalise: if caller passes 0–100, convert to 0–1
        if (pct > 1.0) pct /= 100.0;

        return Math.Max(0, Math.Min(width, pct * width));
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
