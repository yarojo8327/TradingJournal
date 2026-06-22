namespace Application.WPF.Models.Entities;

public class SymbolMapping
{
    public int      Id            { get; set; }
    public string   BrokerSymbol  { get; set; } = string.Empty;
    public string   CanonicalName { get; set; } = string.Empty;
    public string   Category      { get; set; } = string.Empty;

    /// <summary>
    /// $ value (in account currency) per 1.0 price-unit move, for a 1.0 standard lot.
    /// Equivalent to the instrument's contract size when account currency == quote currency.
    /// Null when not yet configured — required for lot-size calculation (HU-TRD-001 Scenario 4).
    /// </summary>
    public decimal? ValuePerPoint { get; set; }
}
