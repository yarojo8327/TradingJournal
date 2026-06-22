using Application.WPF.Infrastructure.Data;
using Application.WPF.Models.Entities;
using Application.WPF.Models.Enums;
using Application.WPF.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.WPF.Services.TradingAccounts;

public class TradingAccountService : ITradingAccountService
{
    private readonly TradingJournalDbContext        _db;
    private readonly ILogger<TradingAccountService> _logger;

    public TradingAccountService(TradingJournalDbContext db, ILogger<TradingAccountService> logger)
    {
        _db     = db;
        _logger = logger;
    }

    public async Task<bool> ExistsForUserAsync(int userId) =>
        await _db.TradingAccounts.AnyAsync(a => a.UserId == userId);

    public async Task<TradingAccount?> GetByUserIdAsync(int userId) =>
        await _db.TradingAccounts.FirstOrDefaultAsync(a => a.UserId == userId);

    public async Task<IReadOnlyList<TradingAccount>> GetAllByUserIdAsync(int userId) =>
        await _db.TradingAccounts
                 .Where(a => a.UserId == userId)
                 .OrderByDescending(a => a.CreatedAt)
                 .ToListAsync();

    public async Task<TradingAccount> CreateAsync(
        int userId, string broker, string accountNumber,
        AccountType accountType, decimal initialCapital,
        string baseCurrency, string leverage, DateTime startDate,
        bool isCentAccount = false, decimal maxRiskPercentPerTrade = 2.0m)
    {
        var account = new TradingAccount
        {
            UserId                 = userId,
            Broker                 = broker.Trim(),
            AccountNumber          = accountNumber.Trim(),
            AccountType            = accountType,
            InitialCapital         = initialCapital,
            BaseCurrency           = baseCurrency.Trim().ToUpper(),
            Leverage               = leverage.Trim(),
            IsCentAccount          = isCentAccount,
            MaxRiskPercentPerTrade = maxRiskPercentPerTrade,
            StartDate              = startDate.Date,
            CreatedAt              = DateTime.UtcNow
        };

        _db.TradingAccounts.Add(account);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Trading account created for user {UserId}", userId);
        return account;
    }

    public async Task<bool> HasTradesAsync(int accountId) =>
        await _db.TradeEntries.AnyAsync(t => t.AccountId == accountId);

    public async Task DeleteAsync(int accountId)
    {
        var account = await _db.TradingAccounts.FindAsync(accountId)
            ?? throw new InvalidOperationException("Cuenta de trading no encontrada.");

        _db.TradingAccounts.Remove(account);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Trading account {Id} deleted", accountId);
    }

    public async Task<TradingAccount> UpdateAsync(
        int accountId, string broker, string accountNumber,
        AccountType accountType, decimal initialCapital,
        string baseCurrency, string leverage, DateTime startDate,
        bool isCentAccount = false, decimal maxRiskPercentPerTrade = 2.0m)
    {
        var account = await _db.TradingAccounts.FindAsync(accountId)
            ?? throw new InvalidOperationException("Cuenta de trading no encontrada.");

        account.Broker                 = broker.Trim();
        account.AccountNumber          = accountNumber.Trim();
        account.AccountType            = accountType;
        account.InitialCapital         = initialCapital;
        account.BaseCurrency           = baseCurrency.Trim().ToUpper();
        account.Leverage               = leverage.Trim();
        account.IsCentAccount          = isCentAccount;
        account.MaxRiskPercentPerTrade = maxRiskPercentPerTrade;
        account.StartDate              = startDate.Date;
        account.UpdatedAt              = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        _logger.LogInformation("Trading account {Id} updated", accountId);
        return account;
    }
}
