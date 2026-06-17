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
    // ── Built-in symbol normalization map (broker alias → canonical) ──────
    private static readonly Dictionary<string, string> BuiltInSymbolMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Indices — common aliases
            ["SP500"]     = "US500",  ["SPX500"]   = "US500",  ["SPX"]     = "US500",
            ["DOW30"]     = "US30",   ["DJIA"]     = "US30",   ["DJI"]     = "US30",   ["WALLST"]  = "US30",
            ["NASDAQ"]    = "NAS100", ["NDX"]      = "NAS100", ["USTEC"]   = "NAS100", ["US100"]   = "NAS100",
            ["DAX"]       = "GER40",  ["GDAXI"]    = "GER40",  ["DE40"]    = "GER40",  ["GER30"]   = "GER40",
            ["FTSE"]      = "UK100",  ["FTSE100"]  = "UK100",  ["GB100"]   = "UK100",
            ["CAC40"]     = "FRA40",  ["CAC"]      = "FRA40",  ["FR40"]    = "FRA40",
            ["STOXX50"]   = "EU50",   ["EUSTX50"]  = "EU50",   ["SX5E"]    = "EU50",
            ["JPN225"]    = "JP225",  ["NIKKEI"]   = "JP225",  ["N225"]    = "JP225",  ["JAPAN225"] = "JP225",
            ["ASX200"]    = "AUS200", ["AU200"]    = "AUS200", ["SPASX200"] = "AUS200",
            ["HANGSENG"]  = "HK50",   ["HSI"]      = "HK50",   ["HKG33"]   = "HK50",
            ["IBEX35"]    = "SPA35",  ["IBEX"]     = "SPA35",  ["ES35"]    = "SPA35",
            ["FTSEMIB"]   = "ITA40",  ["IT40"]     = "ITA40",
            ["SMI"]       = "CH20",   ["SWISS20"]  = "CH20",
            ["DXY"]       = "USDX",   ["DOLLARINDEX"] = "USDX",
            // Commodities — common aliases
            ["GOLD"]      = "XAUUSD", ["GOLDUSD"]  = "XAUUSD", ["XAU"]     = "XAUUSD",
            ["SILVER"]    = "XAGUSD", ["SILVERUSD"] = "XAGUSD", ["XAG"]    = "XAGUSD",
            ["PLATINUM"]  = "XPTUSD", ["XPT"]      = "XPTUSD",
            ["PALLADIUM"] = "XPDUSD", ["XPD"]      = "XPDUSD",
            ["WTICRUDE"]  = "USOIL",  ["CRUDEOIL"] = "USOIL",  ["OIL"]     = "USOIL",
            ["OILUSD"]    = "USOIL",  ["CL"]       = "USOIL",  ["WTI"]     = "USOIL",  ["USCRUDE"] = "USOIL",
            ["BRENT"]     = "UKOIL",  ["BRENTUSD"] = "UKOIL",  ["UKCRUDE"] = "UKOIL",  ["BRENTCRUDE"] = "UKOIL",
            ["NATURALGAS"]= "NATGAS", ["NGAS"]      = "NATGAS", ["NG"]      = "NATGAS",
            ["XUCUSD"]    = "COPPER", ["CU"]        = "COPPER",
        };

    // ── MT5 section markers — bilingual (ES + EN) ─────────────────────────
    private static readonly HashSet<string> ClosedSections =
        new(StringComparer.OrdinalIgnoreCase)
        { "Posiciones", "Positions" };

    private static readonly HashSet<string> OpenSections =
        new(StringComparer.OrdinalIgnoreCase)
        { "Posiciones abiertas", "Open Positions" };

    private static readonly HashSet<string> TerminalSections =
        new(StringComparer.OrdinalIgnoreCase)
        { "Órdenes", "Orders", "Transacciones", "Deals", "Resultados", "Results", "Summary" };

    // ─────────────────────────────────────────────────────────────────────

    public static List<TradeEntryData> Parse(
        string filePath,
        int accountId,
        Dictionary<string, string>? externalMap = null,
        bool isCentAccount = false)
    {
        var result = new List<TradeEntryData>();

        using var wb = new XLWorkbook(filePath);
        var ws = wb.Worksheet(1);

        var section = ParseSection.None;

        foreach (var row in ws.RowsUsed())
        {
            var label = row.Cell(1).GetString().Trim();

            // ── Section transitions ──────────────────────────────────────
            if (ClosedSections.Contains(label))   { section = ParseSection.Closed; continue; }
            if (OpenSections.Contains(label))     { section = ParseSection.Open;   continue; }
            if (TerminalSections.Contains(label)) { section = ParseSection.None;   continue; }

            if (section == ParseSection.None) continue;

            // ── Skip header row (ES: "Fecha"/"Hora" | EN: "Time"/"Open Time") ──
            if (label.StartsWith("Fecha",     StringComparison.OrdinalIgnoreCase) ||
                label.StartsWith("Hora",      StringComparison.OrdinalIgnoreCase) ||
                label.StartsWith("Time",      StringComparison.OrdinalIgnoreCase) ||
                label.StartsWith("Open Time", StringComparison.OrdinalIgnoreCase))
                continue;

            // ── Skip empty / totals rows ─────────────────────────────────
            if (string.IsNullOrWhiteSpace(label)) continue;

            // ── Parse trade row ──────────────────────────────────────────
            var trade = section switch
            {
                ParseSection.Closed => ParseClosedRow(row, accountId, externalMap, isCentAccount),
                ParseSection.Open   => ParseOpenRow(row, accountId, externalMap, isCentAccount),
                _                   => null
            };

            if (trade is not null) result.Add(trade);
        }

        return result;
    }

    // ── Closed positions ──────────────────────────────────────────────────
    // Cols: 1=EntryDT 2=PosID 3=Symbol 4=Type 5=Volume 6=EntryPx
    //       7=SL 8=TP 9=ExitDT 10=ExitPx 11=Comm 12=Swap 13=PnL

    private static TradeEntryData? ParseClosedRow(IXLRow row, int accountId, Dictionary<string, string>? externalMap, bool isCentAccount)
    {
        var entryDate = ParseDate(row.Cell(1).GetString());
        if (entryDate is null) return null;

        var posId      = row.Cell(2).GetString().Trim();
        var symbol     = NormalizeSymbol(row.Cell(3).GetString(), externalMap);
        var direction  = ParseDirection(row.Cell(4).GetString());
        var volume     = GetDecimal(row.Cell(5));
        var entryPrice = GetDecimal(row.Cell(6));
        var sl         = GetDecimal(row.Cell(7));
        var tp         = GetDecimal(row.Cell(8));
        var exitDate   = ParseDate(row.Cell(9).GetString());
        var exitPrice  = GetDecimal(row.Cell(10));
        var rawPnl     = GetDecimalKeepZero(row.Cell(13));
        // Cent accounts report PnL in cents (100× larger); scale to standard units
        var pnl        = isCentAccount && rawPnl.HasValue ? rawPnl.Value / 100m : rawPnl;

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

    private static TradeEntryData? ParseOpenRow(IXLRow row, int accountId, Dictionary<string, string>? externalMap, bool isCentAccount)
    {
        var entryDate = ParseDate(row.Cell(1).GetString());
        if (entryDate is null) return null;

        var posId      = row.Cell(2).GetString().Trim();
        var symbol     = NormalizeSymbol(row.Cell(3).GetString(), externalMap);
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

    private static string NormalizeSymbol(string raw, Dictionary<string, string>? externalMap)
    {
        var s = raw.Trim();

        // 1. User-defined mappings (DB) take priority
        if (externalMap is not null && externalMap.TryGetValue(s, out var ext)) return ext;

        // 2. Built-in alias map
        if (BuiltInSymbolMap.TryGetValue(s, out var builtin)) return builtin;

        // 3. Dot-suffix fallback: strip the suffix and retry (e.g. EURUSD.sc → EURUSD)
        var dot = s.LastIndexOf('.');
        if (dot > 0)
        {
            var baseSym = s[..dot];
            if (externalMap is not null && externalMap.TryGetValue(baseSym, out var extBase)) return extBase;
            if (BuiltInSymbolMap.TryGetValue(baseSym, out var builtinBase)) return builtinBase;
            return baseSym.ToUpper();
        }

        return s.ToUpper();
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
