using Application.WPF.Infrastructure.Data;
using Application.WPF.Models.Enums;
using Application.WPF.Services.TradingAccounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Application.WPF.Tests.TradingAccounts;

public class TradingAccountServiceTests : IDisposable
{
    private readonly TradingJournalDbContext    _db;
    private readonly TradingAccountService     _sut;

    public TradingAccountServiceTests()
    {
        var opts = new DbContextOptionsBuilder<TradingJournalDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db  = new TradingJournalDbContext(opts);
        _sut = new TradingAccountService(_db, NullLogger<TradingAccountService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    // ── ExistsForUserAsync ───────────────────────────────────────────────

    [Fact]
    public async Task ExistsForUser_WhenNoAccount_ReturnsFalse()
    {
        var result = await _sut.ExistsForUserAsync(1);
        Assert.False(result);
    }

    [Fact]
    public async Task ExistsForUser_WhenAccountExists_ReturnsTrue()
    {
        await _sut.CreateAsync(1, "Broker", "ACC001", AccountType.Demo, 5000m, "USD", "1:100", DateTime.Today);

        Assert.True(await _sut.ExistsForUserAsync(1));
    }

    // ── GetByUserIdAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GetByUserId_WhenNoAccount_ReturnsNull()
    {
        var result = await _sut.GetByUserIdAsync(99);
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByUserId_ReturnsCorrectAccount()
    {
        await _sut.CreateAsync(5, "Pepperstone", "ACC999", AccountType.Real, 10000m, "USD", "1:200", DateTime.Today);

        var account = await _sut.GetByUserIdAsync(5);

        Assert.NotNull(account);
        Assert.Equal("Pepperstone", account!.Broker);
        Assert.Equal("ACC999",      account.AccountNumber);
        Assert.Equal(AccountType.Real, account.AccountType);
        Assert.Equal(10000m,        account.InitialCapital);
        Assert.Equal("USD",         account.BaseCurrency);
        Assert.Equal("1:200",       account.Leverage);
    }

    // ── CreateAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task Create_PersistsAccount()
    {
        await _sut.CreateAsync(1, "IC Markets", "12345", AccountType.Prop, 50000m, "eur", "1:50", new DateTime(2025, 1, 1));

        var account = await _db.TradingAccounts.FirstOrDefaultAsync(a => a.UserId == 1);
        Assert.NotNull(account);
    }

    [Fact]
    public async Task Create_NormalizesCurrencyToUpperCase()
    {
        var account = await _sut.CreateAsync(2, "Broker", "ACC", AccountType.Demo, 1000m, "eur", "1:100", DateTime.Today);

        Assert.Equal("EUR", account.BaseCurrency);
    }

    [Fact]
    public async Task Create_StoresOnlyDatePartOfStartDate()
    {
        var input   = new DateTime(2025, 6, 15, 14, 30, 0);
        var account = await _sut.CreateAsync(3, "Broker", "ACC", AccountType.Demo, 1000m, "USD", "1:100", input);

        Assert.Equal(input.Date, account.StartDate);
    }

    [Fact]
    public async Task Create_SetsCreatedAt()
    {
        var before  = DateTime.UtcNow;
        var account = await _sut.CreateAsync(4, "Broker", "ACC", AccountType.Real, 2000m, "USD", "1:100", DateTime.Today);

        Assert.True(account.CreatedAt >= before);
    }

    [Fact]
    public async Task Create_UpdatedAt_IsNull()
    {
        var account = await _sut.CreateAsync(6, "Broker", "ACC", AccountType.Demo, 1000m, "USD", "1:100", DateTime.Today);

        Assert.Null(account.UpdatedAt);
    }

    // ── UpdateAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task Update_ModifiesAccount()
    {
        var created = await _sut.CreateAsync(10, "OldBroker", "OLD001", AccountType.Demo, 1000m, "USD", "1:50", DateTime.Today);

        var updated = await _sut.UpdateAsync(created.Id, "NewBroker", "NEW001", AccountType.Real, 9999m, "eur", "1:200", new DateTime(2026, 1, 1));

        Assert.Equal("NewBroker",     updated.Broker);
        Assert.Equal("NEW001",        updated.AccountNumber);
        Assert.Equal(AccountType.Real, updated.AccountType);
        Assert.Equal(9999m,           updated.InitialCapital);
        Assert.Equal("EUR",           updated.BaseCurrency);
        Assert.Equal("1:200",         updated.Leverage);
        Assert.Equal(new DateTime(2026, 1, 1), updated.StartDate);
    }

    [Fact]
    public async Task Update_SetsUpdatedAt()
    {
        var created = await _sut.CreateAsync(11, "Broker", "ACC", AccountType.Demo, 1000m, "USD", "1:100", DateTime.Today);
        var before  = DateTime.UtcNow;

        var updated = await _sut.UpdateAsync(created.Id, "Broker", "ACC", AccountType.Demo, 1000m, "USD", "1:100", DateTime.Today);

        Assert.NotNull(updated.UpdatedAt);
        Assert.True(updated.UpdatedAt >= before);
    }

    [Fact]
    public async Task Update_WithInvalidId_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.UpdateAsync(9999, "B", "A", AccountType.Demo, 1m, "USD", "1:1", DateTime.Today));
    }
}
