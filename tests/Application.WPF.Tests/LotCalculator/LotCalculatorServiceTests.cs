using Application.WPF.Infrastructure.Data;
using Application.WPF.Models.Entities;
using Application.WPF.Services.Interfaces;
using Application.WPF.Services.LotCalculator;
using Application.WPF.Services.Symbols;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Application.WPF.Tests.LotCalculator;

public class LotCalculatorServiceTests : IDisposable
{
    private readonly TradingJournalDbContext   _db;
    private readonly SymbolMappingService      _symbolService;
    private readonly LotCalculatorService      _sut;

    public LotCalculatorServiceTests()
    {
        var opts = new DbContextOptionsBuilder<TradingJournalDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db            = new TradingJournalDbContext(opts);
        _symbolService = new SymbolMappingService(_db);
        _sut           = new LotCalculatorService(_db, _symbolService);
    }

    public void Dispose() => _db.Dispose();

    private async Task SeedSymbolAsync(string canonical, decimal? valuePerPoint)
    {
        _db.SymbolMappings.Add(new SymbolMapping
        {
            BrokerSymbol  = canonical,
            CanonicalName = canonical,
            Category      = SymbolCategory.Forex,
            ValuePerPoint = valuePerPoint
        });
        await _db.SaveChangesAsync();
    }

    private static LotCalculationRequest BuildRequest(
        string symbol = "EURUSD",
        decimal capital = 10000m,
        decimal riskPercent = 1m,
        decimal entryPrice = 1.1000m,
        decimal stopLoss = 1.0950m,
        decimal? takeProfit = null,
        decimal? maxRiskPercentPerTrade = null) =>
        new(UserId: 1, AccountId: null, Symbol: symbol, Capital: capital, RiskPercent: riskPercent,
            EntryPrice: entryPrice, StopLoss: stopLoss, TakeProfit: takeProfit,
            AccountCurrency: "USD", MaxRiskPercentPerTrade: maxRiskPercentPerTrade);

    // ── RN-001: SL = 0 or Entry = 0 must block the calculation ───────────

    [Fact]
    public async Task Calculate_EntryPriceZero_ReturnsError()
    {
        await SeedSymbolAsync("EURUSD", 100_000m);
        var result = await _sut.CalculateAsync(BuildRequest(entryPrice: 0));

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task Calculate_StopLossZero_ReturnsError()
    {
        await SeedSymbolAsync("EURUSD", 100_000m);
        var result = await _sut.CalculateAsync(BuildRequest(stopLoss: 0));

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task Calculate_EntryEqualsStopLoss_ReturnsError()
    {
        await SeedSymbolAsync("EURUSD", 100_000m);
        var result = await _sut.CalculateAsync(BuildRequest(entryPrice: 1.1000m, stopLoss: 1.1000m));

        Assert.False(result.Success);
    }

    // ── Scenario 4: missing point-value configuration ─────────────────────

    [Fact]
    public async Task Calculate_SymbolWithoutValuePerPoint_ReturnsError()
    {
        await SeedSymbolAsync("NOCONFIG", null);
        var result = await _sut.CalculateAsync(BuildRequest(symbol: "NOCONFIG"));

        Assert.False(result.Success);
        Assert.Contains("valor por punto", result.ErrorMessage);
    }

    [Fact]
    public async Task Calculate_UnknownSymbol_ReturnsError()
    {
        var result = await _sut.CalculateAsync(BuildRequest(symbol: "DOESNOTEXIST"));
        Assert.False(result.Success);
    }

    // ── Scenario 1: correct calculation from capital, risk and SL ────────

    [Fact]
    public async Task Calculate_ValidInputs_ReturnsExpectedLotSize()
    {
        await SeedSymbolAsync("EURUSD", 100_000m);
        // Capital 10000, risk 1% => RiskAmount = 100
        // Distance = |1.1000 - 1.0950| = 0.0050
        // LotSize = 100 / (0.0050 * 100000) = 100 / 500 = 0.20
        var result = await _sut.CalculateAsync(BuildRequest());

        Assert.True(result.Success);
        Assert.Equal(100m, result.RiskAmount);
        Assert.Equal(0.20m, result.LotSize);
    }

    [Fact]
    public async Task Calculate_WithTakeProfit_ReturnsRiskRewardRatio()
    {
        await SeedSymbolAsync("EURUSD", 100_000m);
        // Risk distance 0.0050, reward distance |1.1100-1.1000| = 0.0100 => RR = 2.00
        var result = await _sut.CalculateAsync(BuildRequest(takeProfit: 1.1100m));

        Assert.True(result.Success);
        Assert.Equal(2.00m, result.RiskRewardRatio);
    }

    // ── RN-002: warn (not block) when risk exceeds the account's configured max ──

    [Fact]
    public async Task Calculate_RiskExceedsMax_ReturnsWarningButSucceeds()
    {
        await SeedSymbolAsync("EURUSD", 100_000m);
        var result = await _sut.CalculateAsync(BuildRequest(riskPercent: 5m, maxRiskPercentPerTrade: 2m));

        Assert.True(result.Success);
        Assert.NotNull(result.WarningMessage);
    }

    [Fact]
    public async Task Calculate_RiskWithinMax_NoWarning()
    {
        await SeedSymbolAsync("EURUSD", 100_000m);
        var result = await _sut.CalculateAsync(BuildRequest(riskPercent: 1m, maxRiskPercentPerTrade: 2m));

        Assert.True(result.Success);
        Assert.Null(result.WarningMessage);
    }

    // ── RN-005: account currency is carried through to the result context ─

    [Fact]
    public async Task Calculate_PreservesAccountCurrencyOnSave()
    {
        await SeedSymbolAsync("EURUSD", 100_000m);
        var request = BuildRequest() with { AccountCurrency = "COP" };
        var result  = await _sut.CalculateAsync(request);

        var saved = await _sut.SaveAsync(request, result);
        Assert.Equal("COP", saved.AccountCurrency);
    }

    // ── RN-006: persist successful calculations for audit ────────────────

    [Fact]
    public async Task SaveAsync_PersistsCalculation()
    {
        await SeedSymbolAsync("EURUSD", 100_000m);
        var request = BuildRequest();
        var result  = await _sut.CalculateAsync(request);

        var saved = await _sut.SaveAsync(request, result);

        Assert.True(saved.Id > 0);
        Assert.Equal("EURUSD", saved.Symbol);
        Assert.Equal(0.20m, saved.LotSize);
    }

    [Fact]
    public async Task SaveAsync_WhenResultFailed_Throws()
    {
        var request = BuildRequest(entryPrice: 0);
        var failedResult = await _sut.CalculateAsync(request);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.SaveAsync(request, failedResult));
    }

    [Fact]
    public async Task GetHistoryAsync_ReturnsOnlyUsersCalculations_MostRecentFirst()
    {
        await SeedSymbolAsync("EURUSD", 100_000m);
        var request1 = BuildRequest() with { UserId = 1 };
        var request2 = BuildRequest() with { UserId = 2 };

        var result = await _sut.CalculateAsync(request1);
        await _sut.SaveAsync(request1, result);
        await _sut.SaveAsync(request2, result);

        var history = await _sut.GetHistoryAsync(1);

        Assert.Single(history);
        Assert.Equal(1, history[0].UserId);
    }
}
