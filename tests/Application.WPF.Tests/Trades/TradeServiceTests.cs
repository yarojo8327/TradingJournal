using Application.WPF.Infrastructure.Data;
using Application.WPF.Models.Entities;
using Application.WPF.Models.Enums;
using Application.WPF.Services.Interfaces;
using Application.WPF.Services.Trades;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Application.WPF.Tests.Trades;

public class TradeServiceTests : IDisposable
{
    private readonly TradingJournalDbContext _db;
    private readonly TradeService           _sut;
    private int _accountId;

    public TradeServiceTests()
    {
        var opts = new DbContextOptionsBuilder<TradingJournalDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db  = new TradingJournalDbContext(opts);
        _sut = new TradeService(_db, NullLogger<TradeService>.Instance);

        SeedAsync().GetAwaiter().GetResult();
    }

    private async Task SeedAsync()
    {
        var user = new User
        {
            FullName     = "Test User",
            Email        = "test@example.com",
            Username     = "testuser",
            PasswordHash = "hash",
            CreatedAt    = DateTime.UtcNow
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var account = new TradingAccount
        {
            UserId         = user.Id,
            Broker         = "Pepperstone",
            AccountNumber  = "ACC001",
            AccountType    = AccountType.Demo,
            InitialCapital = 10000m,
            BaseCurrency   = "USD",
            Leverage       = "1:100",
            StartDate      = DateTime.Today,
            CreatedAt      = DateTime.UtcNow
        };
        _db.TradingAccounts.Add(account);
        await _db.SaveChangesAsync();
        _accountId = account.Id;
    }

    public void Dispose() => _db.Dispose();

    private TradeEntryData MakeData(string symbol = "EURUSD") => new(
        AccountId:        _accountId,
        StrategyId:       null,
        Symbol:           symbol,
        Direction:        TradeDirection.Long,
        EntryDate:        DateTime.UtcNow,
        ExitDate:         null,
        EntryPrice:       1.08500m,
        ExitPrice:        null,
        StopLoss:         1.08000m,
        TakeProfit:       1.09500m,
        PositionSizeLots: 0.1m,
        RiskAmount:       50m,
        ProfitLoss:       null,
        PipsResult:       null,
        RiskRewardRatio:  null,
        Result:           TradeResult.Open,
        Session:          TradingSession.London,
        Timeframe:        "1H",
        SetupQuality:     7,
        ConfluencesCount: 3,
        IsFalseBreakout:  false,
        Rating:           8,
        EmotionalState:   EmotionalState.Calm,
        MistakeType:      null,
        Notes:            "Test trade",
        ScreenshotUrl:    null
    );

    // ── CreateAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_PersistsTrade()
    {
        var trade = await _sut.CreateAsync(MakeData());

        Assert.True(trade.Id > 0);
        Assert.Equal("EURUSD",             trade.Symbol);
        Assert.Equal(TradeDirection.Long,   trade.Direction);
        Assert.Equal(TradeResult.Open,      trade.Result);
        Assert.Equal(TradingSession.London, trade.Session);
        Assert.Equal("1H",                 trade.Timeframe);
        Assert.Equal(7,                    trade.SetupQuality);
        Assert.Equal(50m,                  trade.RiskAmount);
    }

    [Fact]
    public async Task CreateAsync_UppercasesSymbol()
    {
        var trade = await _sut.CreateAsync(MakeData("eurusd"));
        Assert.Equal("EURUSD", trade.Symbol);
    }

    // ── GetAllByUserIdAsync ──────────────────────────────────────────────

    [Fact]
    public async Task GetAllByUserId_ReturnsTradesForUser()
    {
        await _sut.CreateAsync(MakeData("EURUSD"));
        await _sut.CreateAsync(MakeData("GBPUSD"));

        var list = await _sut.GetAllByAccountIdAsync(_accountId);

        Assert.Equal(2, list.Count);
    }

    [Fact]
    public async Task GetAllByUserId_OrderedByEntryDateDescending()
    {
        var d1 = MakeData("EURUSD") with { EntryDate = DateTime.UtcNow.AddDays(-2) };
        var d2 = MakeData("GBPUSD") with { EntryDate = DateTime.UtcNow };

        await _sut.CreateAsync(d1);
        await _sut.CreateAsync(d2);

        var list = await _sut.GetAllByAccountIdAsync(_accountId);

        Assert.Equal("GBPUSD", list[0].Symbol);
        Assert.Equal("EURUSD", list[1].Symbol);
    }

    // ── UpdateAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_UpdatesFields()
    {
        var trade = await _sut.CreateAsync(MakeData());
        var updated = MakeData("XAUUSD") with
        {
            ExitPrice      = 1.09000m,
            ProfitLoss     = 50m,
            PipsResult     = 50m,
            RiskRewardRatio = 1.0m,
            Result         = TradeResult.Profit,
            Notes          = "Updated"
        };

        var result = await _sut.UpdateAsync(trade.Id, updated);

        Assert.Equal("XAUUSD",          result.Symbol);
        Assert.Equal(TradeResult.Profit, result.Result);
        Assert.Equal(50m,               result.ProfitLoss);
        Assert.NotNull(result.UpdatedAt);
    }

    // ── DeleteAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_RemovesTrade()
    {
        var trade = await _sut.CreateAsync(MakeData());
        await _sut.DeleteAsync(trade.Id);

        var found = await _sut.GetByIdAsync(trade.Id);
        Assert.Null(found);
    }

    [Fact]
    public async Task DeleteAsync_ThrowsWhenNotFound()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.DeleteAsync(9999));
    }

    // ── GetByIdAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_ReturnsNull_WhenNotFound()
    {
        var result = await _sut.GetByIdAsync(9999);
        Assert.Null(result);
    }

    [Fact]
    public async Task GetById_IncludesAccountNavigation()
    {
        var trade = await _sut.CreateAsync(MakeData());
        var result = await _sut.GetByIdAsync(trade.Id);

        Assert.NotNull(result);
        Assert.NotNull(result!.Account);
        Assert.Equal("Pepperstone", result.Account.Broker);
    }
}
