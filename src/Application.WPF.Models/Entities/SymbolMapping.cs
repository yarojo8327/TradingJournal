namespace Application.WPF.Models.Entities;

public class SymbolMapping
{
    public int    Id            { get; set; }
    public string BrokerSymbol  { get; set; } = string.Empty;
    public string CanonicalName { get; set; } = string.Empty;
    public string Category      { get; set; } = string.Empty;
}
