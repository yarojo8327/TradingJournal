using Application.WPF.Models.Enums;
using Application.WPF.Services.Interfaces;
using ClosedXML.Excel;
using System.Globalization;

namespace Application.WPF.ViewModels.Journal;

/// <summary>
/// Parses a MetaTrader 5 "Historial de trading" Excel report (.xlsx)
/// exported from any MT5 broker and converts it to <see cref="TradeEntryData"/> records.
/// </summary>
internal static class Mt5ReportParser
{
    // ── Symbol normalization: broker suffixes → standard symbols ──────────
    private static readonly Dictionary<string, string> SymbolMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["SP500.r"]  = "US500",  ["SP500"]   = "US500",
            ["NAS100.r"] = "NAS100", ["NASDAQ"]  = "NAS100",
            ["US30.r"]   = "US30",   ["DJIA"]    = "US30",
            ["UK100.r"]  = "UK100",  ["FTSE"]    = "UK100",
            ["GER40.r"]  = "GER40",  ["DAX"]     = "GER40",
            ["JP225.r"]  = "JP225",
            ["AUS200.r"] = "AUS200",
            ["HK50.r"]   = "HK50",
            ["FRA40.r"]  = "FRA40",
            ["EU50.r"]   = "EU50",
            ["USOIL.r"]  = "USOIL",  ["USOIL.m"] = "USOIL",
            ["UKOIL.r"]  = "UKOIL",  ["UKOIL.m"] = "UKOIL",
            ["XAUUSD.r"] = "XAUUSD",
            ["XAGUSD.r"] = "XAGUSD",
            ["BTCUSD.r"] = "BTCUSD", ["BTCUSD.m"] = "BTCUSD",
            ["ETHUSD.r"] = "ETHUSD", ["ETHUSD.m"] = "ETHUSD",
        };

    // ── MT5 section markers (Spanish report) ─────────────────────────────
    private const string SectionClosed  = "Posiciones";
    private const string SectionOpen    = "Posiciones abiertas";
    private const string SectionOrders  = "Órdenes";
    private const string SectionDeals   = "Transacciones";
    private const string SectionResults = "Resultados";

    // ─────────────────────────────────────────────────────────────────────

    public static List<TradeEntryData> Parse(string filePath, int accountId)
    {
        var result = new List<TradeEntryData>();

        using var wb = new XLWorkbook(filePath);
        var ws = wb.Worksheet(1);

        var section = ParseSection.None;

        foreach (var row in ws.RowsUsed())
        {
            var label = row.Cell(1).GetString().Trim();

            // ── Section transitions ──────────────────────────────────────
            if (label.Equals(SectionClosed,  StringComparison.OrdinalIgnoreCase)) { section = ParseSection.Closed;  continue; }
            if (label.Equals(SectionOpen,    StringComparison.OrdinalIgnoreCase)) { section = ParseSection.Open;    continue; }
            if (label.Equals(SectionOrders,  StringComparison.OrdinalIgnoreCase) ||
                label.Equals(SectionDeals,   StringComparison.OrdinalIgnoreCase) ||
                label.Equals(SectionResults, StringComparison.OrdinalIgnoreCase))
            {
                section = ParseSection.None;
                continue;
            }

            if (section == ParseSection.None) continue;

            // ── Skip header row (starts with "Fecha" / "Hora") ───────────
            if (label.StartsWith("Fecha", StringComparison.OrdinalIgnoreCase) ||
                label.StartsWith("Hora",  StringComparison.OrdinalIgnoreCase))
                continue;

            // ── Skip empty / totals rows ─────────────────────────────────
            if (string.IsNullOrWhiteSpace(label)) continue;

            // ── Parse trade row ──────────────────────────────────────────
            var trade = section switch
            {
                ParseSection.Closed => ParseClosedRow(row, accountId),
                ParseSection.Open   => ParseOpenRow(row, accountId),
                _                   => null
            };

            if (trade is not null) result.Add(trade);
        }

        return result;
    }

    // ── Closed positions ──────────────────────────────────────────────────
    // Cols: 1=EntryDT 2=PosID 3=Symbol 4=Type 5=Volume 6=EntryPx
    //       7=SL 8=TP 9=ExitDT 10=ExitPx 11=Comm 12=Swap 13=PnL

    private static TradeEntryData? ParseClosedRow(IXLRow row, int accountId)
    {
        var entryDate = ParseDate(row.Cell(1).GetString());
        if (entryDate is null) return null;

        var posId      = row.Cell(2).GetString().Trim();
        var symbol     = NormalizeSymbol(row.Cell(3).GetString());
        var direction  = ParseDirection(row.Cell(4).GetString());
        var volume     = GetDecimal(row.Cell(5));
        var entryPrice = GetDecimal(row.Cell(6));
        var sl         = GetDecimal(row.Cell(7));
        var tp         = GetDecimal(row.Cell(8));
        var exitDate   = ParseDate(row.Cell(9).GetString());
        var exitPrice  = GetDecimal(row.Cell(10));
        var pnl        = GetDecimalKeepZero(row.Cell(13));

        if (entryPrice is null || string.IsNullOrEmpty(symbol)) return null;

        var result = pnl switch
        {
            > 0m  => TradeResult.Profit,
            < 0m  => TradeResult.Loss,
            0m    => TradeResult.BreakEven,
            _     => TradeResult.Open
        };

        var notes = string.IsNullOrEmpty(posId) ? null : $"MT5 #{posId}";

        return new TradeEntryData(
            AccountId:        accountId,
            StrategyId:       null,
            Symbol:           symbol,
            Direction:        direction,
            EntryDate:        entryDate.Value,
            ExitDate:         exitDate,
            EntryPrice:       entryPrice.Value,
            ExitPrice:        exitPrice,
            StopLoss:         sl,
            TakeProfit:       tp,
            PositionSizeLots: volume,
            RiskAmount:       null,
            ProfitLoss:       pnl,
            PipsResult:       null,
            RiskRewardRatio:  null,
            Result:           result,
            Session:          null,
            Timeframe:        null,
            TradingType:      null,
            SetupQuality:     null,
            ConfluencesCount: null,
            IsFalseBreakout:  false,
            Rating:           null,
            EmotionalState:   null,
            MistakeType:      null,
            Notes:            notes,
            ScreenshotUrl:    null);
    }

    // ── Open positions ────────────────────────────────────────────────────
    // Cols: 1=EntryDT 2=PosID 3=Symbol 4=Type 5=Volume 6=EntryPx
    //       7=SL 8=TP  (9=MarketPx 10=Swap 12=FloatPnL — all ignored)

    private static TradeEntryData? ParseOpenRow(IXLRow row, int accountId)
    {
        var entryDate = ParseDate(row.Cell(1).GetString());
        if (entryDate is null) return null;

        var posId      = row.Cell(2).GetString().Trim();
        var symbol     = NormalizeSymbol(row.Cell(3).GetString());
        var direction  = ParseDirection(row.Cell(4).GetString());
        var volume     = GetDecimal(row.Cell(5));
        var entryPrice = GetDecimal(row.Cell(6));
        var sl         = GetDecimal(row.Cell(7));
        var tp         = GetDecimal(row.Cell(8));

        if (entryPrice is null || string.IsNullOrEmpty(symbol)) return null;

        var notes = string.IsNullOrEmpty(posId) ? null : $"MT5 #{posId}";

        return new TradeEntryData(
            AccountId:        accountId,
            StrategyId:       null,
            Symbol:           symbol,
            Direction:        direction,
            EntryDate:        entryDate.Value,
            ExitDate:         null,
            EntryPrice:       entryPrice.Value,
            ExitPrice:        null,
            StopLoss:         sl,
            TakeProfit:       tp,
            PositionSizeLots: volume,
            RiskAmount:       null,
            ProfitLoss:       null,
            PipsResult:       null,
            RiskRewardRatio:  null,
            Result:           TradeResult.Open,
            Session:          null,
            Timeframe:        null,
            TradingType:      null,
            SetupQuality:     null,
            ConfluencesCount: null,
            IsFalseBreakout:  false,
            Rating:           null,
            EmotionalState:   null,
            MistakeType:      null,
            Notes:            notes,
            ScreenshotUrl:    null);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static string NormalizeSymbol(string raw)
    {
        var s = raw.Trim();
        return SymbolMap.TryGetValue(s, out var mapped) ? mapped : s.ToUpper();
    }

    private static TradeDirection ParseDirection(string raw) =>
        raw.Trim().Equals("sell", StringComparison.OrdinalIgnoreCase)
            ? TradeDirection.Short
            : TradeDirection.Long;

    // MT5 date format: "yyyy.MM.dd HH:mm:ss"
    private static readonly string[] DateFormats =
        new[] { "yyyy.MM.dd HH:mm:ss", "yyyy.MM.dd HH:mm", "yyyy.MM.dd" };

    private static DateTime? ParseDate(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        return DateTime.TryParseExact(s.Trim(), DateFormats,
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt) ? dt : null;
    }

    /// <summary>
    /// Reads a decimal from a cell safely.
    /// Numeric cells in ClosedXML 0.102 expose their raw double via
    /// <c>cell.Value.GetNumber()</c>  — using <c>GetString()</c> would apply
    /// the cell's number format (e.g. "0") and silently drop decimal places.
    /// </summary>
    private static decimal? GetDecimal(IXLCell cell)
    {
        var v = cell.Value;
        if (v.IsBlank) return null;
        if (v.IsNumber)
        {
            var d = v.GetNumber();
            return d == 0d ? (decimal?)null : (decimal)d;
        }
        // Text cell fallback (some cells may store numbers as text)
        var s = v.IsText ? v.GetText()?.Trim() : cell.GetString()?.Trim();
        if (string.IsNullOrWhiteSpace(s)) return null;
        return decimal.TryParse(s, NumberStyles.Any,
            CultureInfo.InvariantCulture, out var dec) ? dec : null;
    }

    /// <summary>Same as <see cref="GetDecimal"/> but keeps zero values (e.g. PnL = 0 → BreakEven).</summary>
    private static decimal? GetDecimalKeepZero(IXLCell cell)
    {
        var v = cell.Value;
        if (v.IsBlank) return null;
        if (v.IsNumber) return (decimal)v.GetNumber();
        var s = v.IsText ? v.GetText()?.Trim() : cell.GetString()?.Trim();
        if (string.IsNullOrWhiteSpace(s)) return null;
        return decimal.TryParse(s, NumberStyles.Any,
            CultureInfo.InvariantCulture, out var dec) ? dec : null;
    }

    // ─────────────────────────────────────────────────────────────────────

    private enum ParseSection { None, Closed, Open }
}
