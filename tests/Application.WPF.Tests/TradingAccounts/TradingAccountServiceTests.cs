using Application.WPF.Infrastructure.Data;
using Application.WPF.Models.Enums;
using Application.WPF.Services.TradingAccounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Application.WPF.Tests.TradingAccounts;

public class TradingAccountServiceTests : IDisposable
{
    private readonly TradingJournalDbContext _db;
    private readonly TradingAccountService  _sut;

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
        Assert.False(await _sut.ExistsForUserAsync(1));
    }

    [Fact]
    public async Task ExistsForUser_WhenAccountExists_ReturnsTrue()
    {
        await CreateAccount(1);
        Assert.True(await _sut.ExistsForUserAsync(1));
    }

    // ── GetByUserIdAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GetByUserId_WhenNoAccount_ReturnsNull()
    {
        Assert.Null(await _sut.GetByUserIdAsync(99));
    }

    [Fact]
    public async Task GetByUserId_ReturnsCorrectAccount()
    {
        await _sut.CreateAsync(5, "Pepperstone", "ACC999", AccountType.Real,
                               10000m, "USD", "1:200", DateTime.Today);

        var account = await _sut.GetByUserIdAsync(5);

        Assert.NotNull(account);
        Assert.Equal("Pepperstone",    account!.Broker);
        Assert.Equal(AccountType.Real, account.AccountType);
        Assert.Equal("USD",            account.BaseCurrency);
    }

    // ── GetAllByUserIdAsync ──────────────────────────────────────────────

    [Fact]
    public async Task GetAllByUserId_WhenNoAccounts_ReturnsEmptyList()
    {
        var result = await _sut.GetAllByUserIdAsync(1);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllByUserId_ReturnsOnlyAccountsOfThatUser()
    {
        await CreateAccount(1, "BrokerA");
        await CreateAccount(1, "BrokerB");
        await CreateAccount(2, "BrokerC");

        var result = await _sut.GetAllByUserIdAsync(1);

        Assert.Equal(2, result.Count);
        Assert.All(result, a => Assert.Equal(1, a.UserId));
    }

    [Fact]
    public async Task GetAllByUserId_ReturnsOrderedByCreatedAtDescending()
    {
        var first  = await CreateAccount(3, "First");
        await Task.Delay(5);
        var second = await CreateAccount(3, "Second");

        var result = await _sut.GetAllByUserIdAsync(3);

        Assert.Equal("Second", result[0].Broker);
        Assert.Equal("First",  result[1].Broker);
    }

    [Fact]
    public async Task GetAllByUserId_AllowsMultipleAccountsPerUser()
    {
        await CreateAccount(4, "BrokerA");
        await CreateAccount(4, "BrokerB");
        await CreateAccount(4, "BrokerC");

        var result = await _sut.GetAllByUserIdAsync(4);

        Assert.Equal(3, result.Count);
    }

    // ── CreateAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task Create_PersistsAccount()
    {
        await _sut.CreateAsync(1, "IC Markets", "12345", AccountType.Prop,
                               50000m, "eur", "1:50", new DateTime(2025, 1, 1));

        Assert.True(await _db.TradingAccounts.AnyAsync(a => a.UserId == 1));
    }

    [Fact]
    public async Task Create_NormalizesCurrencyToUpperCase()
    {
        var account = await CreateAccount(2, currency: "eur");
        Assert.Equal("EUR", account.BaseCurrency);
    }

    [Fact]
    public async Task Create_StoresOnlyDatePartOfStartDate()
    {
        var input   = new DateTime(2025, 6, 15, 14, 30, 0);
        var account = await _sut.CreateAsync(3, "Broker", "ACC", AccountType.Demo,
                                             1000m, "USD", "1:100", input);
        Assert.Equal(input.Date, account.StartDate);
    }

    [Fact]
    public async Task Create_SetsCreatedAt()
    {
        var before  = DateTime.UtcNow;
        var account = await CreateAccount(4);
        Assert.True(account.CreatedAt >= before);
    }

    [Fact]
    public async Task Create_UpdatedAt_IsNull()
    {
        var account = await CreateAccount(6);
        Assert.Null(account.UpdatedAt);
    }

    // ── UpdateAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task Update_ModifiesAccount()
    {
        var created = await CreateAccount(10, "OldBroker", "OLD001");

        var updated = await _sut.UpdateAsync(created.Id, "NewBroker", "NEW001",
                          AccountType.Real, 9999m, "eur", "1:200", new DateTime(2026, 1, 1));

        Assert.Equal("NewBroker",      updated.Broker);
        Assert.Equal("NEW001",         updated.AccountNumber);
        Assert.Equal(AccountType.Real, updated.AccountType);
        Assert.Equal(9999m,            updated.InitialCapital);
        Assert.Equal("EUR",            updated.BaseCurrency);
        Assert.Equal("1:200",          updated.Leverage);
    }

    [Fact]
    public async Task Update_SetsUpdatedAt()
    {
        var created = await CreateAccount(11);
        var before  = DateTime.UtcNow;

        var updated = await _sut.UpdateAsync(created.Id, "Broker", "ACC",
                          AccountType.Demo, 1000m, "USD", "1:100", DateTime.Today);

        Assert.NotNull(updated.UpdatedAt);
        Assert.True(updated.UpdatedAt >= before);
    }

    [Fact]
    public async Task Update_WithInvalidId_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.UpdateAsync(9999, "B", "A", AccountType.Demo, 1m, "USD", "1:1", DateTime.Today));
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private Task<Models.Entities.TradingAccount> CreateAccount(
        int userId,
        string broker       = "Broker",
        string accountNum   = "ACC001",
        string currency     = "USD") =>
        _sut.CreateAsync(userId, broker, accountNum, AccountType.Demo,
                         5000m, currency, "1:100", DateTime.Today);
}
