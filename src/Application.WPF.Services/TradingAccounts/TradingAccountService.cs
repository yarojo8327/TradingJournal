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
        string baseCurrency, string leverage, DateTime startDate)
    {
        var account = new TradingAccount
        {
            UserId         = userId,
            Broker         = broker.Trim(),
            AccountNumber  = accountNumber.Trim(),
            AccountType    = accountType,
            InitialCapital = initialCapital,
            BaseCurrency   = baseCurrency.Trim().ToUpper(),
            Leverage       = leverage.Trim(),
            StartDate      = startDate.Date,
            CreatedAt      = DateTime.UtcNow
        };

        _db.TradingAccounts.Add(account);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Trading account created for user {UserId}", userId);
        return account;
    }

    public async Task<TradingAccount> UpdateAsync(
        int accountId, string broker, string accountNumber,
        AccountType accountType, decimal initialCapital,
        string baseCurrency, string leverage, DateTime startDate)
    {
        var account = await _db.TradingAccounts.FindAsync(accountId)
            ?? throw new InvalidOperationException("Cuenta de trading no encontrada.");

        account.Broker         = broker.Trim();
        account.AccountNumber  = accountNumber.Trim();
        account.AccountType    = accountType;
        account.InitialCapital = initialCapital;
        account.BaseCurrency   = baseCurrency.Trim().ToUpper();
        account.Leverage       = leverage.Trim();
        account.StartDate      = startDate.Date;
        account.UpdatedAt      = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        _logger.LogInformation("Trading account {Id} updated", accountId);
        return account;
    }
}
