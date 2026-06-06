using Application.WPF.Models.Enums;
using System.Globalization;
using System.Windows.Data;

namespace Application.WPF.Views.Converters;

[ValueConversion(typeof(TradingSession?), typeof(string))]
public class TradingSessionDisplayConverter : IValueConverter
{
    private static readonly Dictionary<TradingSession, string> _map = new()
    {
        { TradingSession.Asian,         "Asiática" },
        { TradingSession.London,        "Londres" },
        { TradingSession.NewYork,       "Nueva York" },
        { TradingSession.Sydney,        "Sydney" },
        { TradingSession.LondonNewYork, "Londres / NY" }
    };

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is TradingSession s && _map.TryGetValue(s, out var display) ? display : "—";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
