using Application.WPF.Models.Entities;
using Application.WPF.Models.Enums;

namespace Application.WPF.Services.Interfaces;

public interface ITradingAccountService
{
    Task<bool>                          ExistsForUserAsync(int userId);
    Task<TradingAccount?>               GetByUserIdAsync(int userId);
    Task<IReadOnlyList<TradingAccount>> GetAllByUserIdAsync(int userId);
    Task<TradingAccount>  CreateAsync(int userId, string broker, string accountNumber,
                                      AccountType accountType, decimal initialCapital,
                                      string baseCurrency, string leverage, DateTime startDate,
                                      bool isCentAccount = false);
    Task<TradingAccount>  UpdateAsync(int accountId, string broker, string accountNumber,
                                      AccountType accountType, decimal initialCapital,
                                      string baseCurrency, string leverage, DateTime startDate,
                                      bool isCentAccount = false);

    /// <summary>
    /// Returns true if the account has at least one trade registered in the journal.
    /// Used to block deletion.
    /// </summary>
    Task<bool> HasTradesAsync(int accountId);

    Task DeleteAsync(int accountId);
}
