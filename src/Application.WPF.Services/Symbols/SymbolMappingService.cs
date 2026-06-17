using Application.WPF.Infrastructure.Data;
using Application.WPF.Models.Entities;
using Application.WPF.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Application.WPF.Services.Symbols;

public class SymbolMappingService : ISymbolMappingService
{
    private readonly TradingJournalDbContext _db;

    public SymbolMappingService(TradingJournalDbContext db) => _db = db;

    public async Task<IReadOnlyList<SymbolMapping>> GetAllAsync() =>
        await _db.SymbolMappings.OrderBy(s => s.Category).ThenBy(s => s.CanonicalName).ThenBy(s => s.BrokerSymbol).ToListAsync();

    public async Task<Dictionary<string, string>> GetMappingDictionaryAsync()
    {
        var all = await _db.SymbolMappings.ToListAsync();
        return all.ToDictionary(s => s.BrokerSymbol, s => s.CanonicalName, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<SymbolMapping> CreateAsync(string brokerSymbol, string canonicalName, string category)
    {
        var item = new SymbolMapping
        {
            BrokerSymbol  = brokerSymbol.Trim().ToUpper(),
            CanonicalName = canonicalName.Trim().ToUpper(),
            Category      = category
        };
        _db.SymbolMappings.Add(item);
        await _db.SaveChangesAsync();
        return item;
    }

    public async Task UpdateAsync(int id, string brokerSymbol, string canonicalName, string category)
    {
        var item = await _db.SymbolMappings.FindAsync(id)
                   ?? throw new InvalidOperationException($"SymbolMapping {id} not found");
        item.BrokerSymbol  = brokerSymbol.Trim().ToUpper();
        item.CanonicalName = canonicalName.Trim().ToUpper();
        item.Category      = category;
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var item = await _db.SymbolMappings.FindAsync(id);
        if (item is null) return;
        _db.SymbolMappings.Remove(item);
        await _db.SaveChangesAsync();
    }

    public async Task EnsureDefaultsAsync()
    {
        if (await _db.SymbolMappings.AnyAsync()) return;

        var defaults = BuildDefaultMappings();
        _db.SymbolMappings.AddRange(defaults);
        await _db.SaveChangesAsync();
    }

    // ── Default seed data ─────────────────────────────────────────────────
    private static IEnumerable<SymbolMapping> BuildDefaultMappings()
    {
        var items = new List<SymbolMapping>();

        void Add(string broker, string canonical, string category) =>
            items.Add(new SymbolMapping { BrokerSymbol = broker, CanonicalName = canonical, Category = category });

        // Suffixes applied to every base symbol
        string[] brokerSuffixes = { ".r", ".sc", ".m", ".pro", ".ecn", ".s", ".raw", ".std", ".i", ".o", ".stp" };

        // ── Forex majors ──────────────────────────────────────────────────
        string[] forexMajors =
        {
            "EURUSD", "GBPUSD", "USDJPY", "USDCHF", "AUDUSD", "USDCAD", "NZDUSD",
            "USDSEK", "USDNOK", "USDDKK"
        };

        // ── Forex minors / crosses ────────────────────────────────────────
        string[] forexMinors =
        {
            "EURGBP", "EURJPY", "EURCHF", "EURAUD", "EURCAD", "EURNZD",
            "EURPLN", "EURHUF", "EURCZK", "EURTRY", "EURMXN", "EURZAR",
            "EURNOK", "EURSEK", "EURHKD", "EURSGD",
            "GBPJPY", "GBPCHF", "GBPAUD", "GBPCAD", "GBPNZD",
            "GBPSGD", "GBPZAR", "GBPTRY", "GBPMXN", "GBPNOK", "GBPSEK",
            "AUDJPY", "AUDCAD", "AUDCHF", "AUDNZD", "AUDSGD", "AUDNOK",
            "CADJPY", "CADCHF", "CADSGD",
            "NZDJPY", "NZDCAD", "NZDCHF", "NZDSGD",
            "CHFJPY", "CHFSGD",
            "SGDJPY", "HKDJPY", "NOKJPY", "SEKJPY", "DKKNOK",
            "USDSGD", "USDHKD", "USDTRY", "USDMXN", "USDZAR",
            "USDPLN", "USDHUF", "USDCZK", "USDTHB", "USDCNH", "USDINR",
            "MXNJPY", "NOKSEK", "ZARJPY"
        };

        // ── Indices ───────────────────────────────────────────────────────
        var indices = new Dictionary<string, string[]>
        {
            ["US500"]  = new[] { "SP500", "SPX500", "US500", "S&P500", "SPX" },
            ["US30"]   = new[] { "US30", "DJIA", "DJI", "DOW30", "WALLST" },
            ["NAS100"] = new[] { "NAS100", "NASDAQ", "NDX", "USTEC", "US100", "NASDAQ100" },
            ["GER40"]  = new[] { "GER40", "DAX", "GDAXI", "DE40", "GER30", "GER30" },
            ["UK100"]  = new[] { "UK100", "FTSE", "FTSE100", "GB100" },
            ["FRA40"]  = new[] { "FRA40", "CAC40", "CAC", "FR40" },
            ["EU50"]   = new[] { "EU50", "STOXX50", "EUSTX50", "SX5E" },
            ["JP225"]  = new[] { "JP225", "JPN225", "NIKKEI", "N225", "JAPAN225" },
            ["AUS200"] = new[] { "AUS200", "AU200", "ASX200", "AUS200", "SPASX200" },
            ["HK50"]   = new[] { "HK50", "HANGSENG", "HSI", "HKG33" },
            ["SPA35"]  = new[] { "SPA35", "IBEX35", "IBEX", "ES35", "SPA35" },
            ["ITA40"]  = new[] { "ITA40", "FTSEMIB", "IT40" },
            ["CH20"]   = new[] { "CH20", "SMI", "SWISS20" },
            ["USDX"]   = new[] { "USDX", "DXY", "DOLLARINDEX" },
            ["VIX"]    = new[] { "VIX", "USVIX", "US500VIX" },
        };

        // ── Commodities ───────────────────────────────────────────────────
        var commodities = new Dictionary<string, string[]>
        {
            ["XAUUSD"] = new[] { "XAUUSD", "GOLD", "GOLDUSD", "XAU" },
            ["XAGUSD"] = new[] { "XAGUSD", "SILVER", "SILVERUSD", "XAG" },
            ["XPTUSD"] = new[] { "XPTUSD", "PLATINUM", "XPT" },
            ["XPDUSD"] = new[] { "XPDUSD", "PALLADIUM", "XPD" },
            ["USOIL"]  = new[] { "USOIL", "WTICRUDE", "CRUDEOIL", "OIL", "OILUSD", "CL", "WTI", "USCRUDE" },
            ["UKOIL"]  = new[] { "UKOIL", "BRENT", "BRENTUSD", "UKCRUDE", "BRENTCRUDE" },
            ["NATGAS"] = new[] { "NATGAS", "NATURALGAS", "NGAS", "NG" },
            ["COPPER"] = new[] { "COPPER", "XUCUSD", "CU" },
            ["CORN"]   = new[] { "CORN", "ZC" },
            ["WHEAT"]  = new[] { "WHEAT", "ZW" },
            ["SOYBEAN"]= new[] { "SOYBEAN", "SOY", "ZS" },
            ["COFFEE"] = new[] { "COFFEE", "KC" },
            ["SUGAR"]  = new[] { "SUGAR", "SB" },
            ["COTTON"] = new[] { "COTTON", "CT" },
            ["COCOA"]  = new[] { "COCOA", "CC" },
        };

        // ── Crypto ────────────────────────────────────────────────────────
        string[] cryptoPairs =
        {
            "BTCUSD", "ETHUSD", "BNBUSD", "XRPUSD", "SOLUSD", "ADAUSD",
            "DOGEUSD", "MATICUSD", "LINKUSD", "LTCUSD", "UNIUSD", "XLMUSD",
            "BCHUSD", "AVAXUSD", "DOTUSD", "ATOMUSD", "ALGOUSD", "NEARUSD",
            "FILUSD", "TRXUSD", "ETCUSD", "XMRUSD", "ZECUSD", "EOSUSD",
            "AAVEUSD", "CRVUSD", "FTMUSD", "SANDUSD", "MANAUSD", "APEUSD",
            "IOTUSD", "NEOUSD", "AXSUSD", "SNXUSD", "MKRUSD", "YFIUSD",
            "COMPUSD", "RENUSD", "UMAUSD", "SUSHIUSD", "KSMUSD", "ANKRUSD",
        };

        // ── Populate: Forex majors ────────────────────────────────────────
        foreach (var sym in forexMajors)
        {
            Add(sym, sym, SymbolCategory.Forex);
            foreach (var sfx in brokerSuffixes)
                Add(sym + sfx, sym, SymbolCategory.Forex);
        }

        // ── Populate: Forex minors ────────────────────────────────────────
        foreach (var sym in forexMinors)
        {
            Add(sym, sym, SymbolCategory.Forex);
            foreach (var sfx in brokerSuffixes)
                Add(sym + sfx, sym, SymbolCategory.Forex);
        }

        // ── Populate: Indices ─────────────────────────────────────────────
        foreach (var (canonical, aliases) in indices)
        {
            Add(canonical, canonical, SymbolCategory.Index);
            foreach (var alias in aliases)
            {
                if (!alias.Equals(canonical, StringComparison.OrdinalIgnoreCase))
                    Add(alias, canonical, SymbolCategory.Index);
                foreach (var sfx in brokerSuffixes)
                    Add(alias + sfx, canonical, SymbolCategory.Index);
            }
        }

        // ── Populate: Commodities ─────────────────────────────────────────
        foreach (var (canonical, aliases) in commodities)
        {
            Add(canonical, canonical, SymbolCategory.Commodity);
            foreach (var alias in aliases)
            {
                if (!alias.Equals(canonical, StringComparison.OrdinalIgnoreCase))
                    Add(alias, canonical, SymbolCategory.Commodity);
                foreach (var sfx in brokerSuffixes)
                    Add(alias + sfx, canonical, SymbolCategory.Commodity);
            }
        }

        // ── Populate: Crypto ──────────────────────────────────────────────
        foreach (var sym in cryptoPairs)
        {
            Add(sym, sym, SymbolCategory.Crypto);
            foreach (var sfx in brokerSuffixes)
                Add(sym + sfx, sym, SymbolCategory.Crypto);
        }

        // Deduplicate by BrokerSymbol (keep first occurrence)
        return items
            .GroupBy(i => i.BrokerSymbol, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First());
    }
}
