using Application.WPF.Models.Entities;
using System.Globalization;
using System.Windows.Data;

namespace Application.WPF.Views.Converters;

/// <summary>
/// Recibe un <see cref="TradeEntry"/> y devuelve su P&amp;L como porcentaje
/// del capital inicial de la cuenta (ej: "+2.35 %").
/// </summary>
public class TradePctConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not TradeEntry trade) return "—";
        if (!trade.ProfitLoss.HasValue)   return "—";
        if (trade.Account is null || trade.Account.InitialCapital <= 0) return "—";

        var pct = (double)(trade.ProfitLoss.Value / trade.Account.InitialCapital * 100m);
        return pct >= 0
            ? $"+{pct:F2} %"
            : $"{pct:F2} %";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
