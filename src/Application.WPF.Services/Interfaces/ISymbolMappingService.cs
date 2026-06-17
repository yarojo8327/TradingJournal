using Application.WPF.Models.Entities;

namespace Application.WPF.Services.Interfaces;

public interface ISymbolMappingService
{
    Task<IReadOnlyList<SymbolMapping>> GetAllAsync();
    Task<Dictionary<string, string>>  GetMappingDictionaryAsync();
    Task<SymbolMapping>               CreateAsync(string brokerSymbol, string canonicalName, string category);
    Task                              UpdateAsync(int id, string brokerSymbol, string canonicalName, string category);
    Task                              DeleteAsync(int id);
    Task                              EnsureDefaultsAsync();
}

public static class SymbolCategory
{
    public const string Forex     = "Forex";
    public const string Index     = "Index";
    public const string Commodity = "Commodity";
    public const string Crypto    = "Crypto";
    public const string Other     = "Other";
}
