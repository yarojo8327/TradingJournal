using Application.WPF.Models.Entities;

namespace Application.WPF.Services.Interfaces;

public interface ISymbolMappingService
{
    Task<IReadOnlyList<SymbolMapping>> GetAllAsync();
    Task<IReadOnlyList<string>>        GetCanonicalNamesAsync();
    Task<Dictionary<string, string>>  GetMappingDictionaryAsync();
    Task<SymbolMapping>               CreateAsync(string brokerSymbol, string canonicalName, string category, decimal? valuePerPoint = null);
    Task                              UpdateAsync(int id, string brokerSymbol, string canonicalName, string category, decimal? valuePerPoint = null);
    Task                              DeleteAsync(int id);
    Task                              EnsureDefaultsAsync();

    /// <summary>$ value per 1.0 price-unit move for a 1.0 lot of this canonical instrument, or null if not configured.</summary>
    Task<decimal?>                    GetValuePerPointAsync(string canonicalName);

    /// <summary>Sets ValuePerPoint on every SymbolMapping row sharing this canonical name.</summary>
    Task                              SetValuePerPointAsync(string canonicalName, decimal valuePerPoint);
}

public static class SymbolCategory
{
    public const string Forex     = "Forex";
    public const string Index     = "Index";
    public const string Commodity = "Commodity";
    public const string Crypto    = "Crypto";
    public const string Other     = "Other";
}
