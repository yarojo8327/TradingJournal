using Application.WPF.Models.Entities;
using Application.WPF.Models.Enums;

namespace Application.WPF.Services.Interfaces;

public interface ITradingAccountService
{
    Task<bool>            ExistsForUserAsync(int userId);
    Task<TradingAccount?> GetByUserIdAsync(int userId);
    Task<TradingAccount>  CreateAsync(int userId, string broker, string accountNumber,
                                      AccountType accountType, decimal initialCapital,
                                      string baseCurrency, string leverage, DateTime startDate);
    Task<TradingAccount>  UpdateAsync(int accountId, string broker, string accountNumber,
                                      AccountType accountType, decimal initialCapital,
                                      string baseCurrency, string leverage, DateTime startDate);
}
