using Application.WPF.Models.Entities;

namespace Application.WPF.Services.Interfaces;

public interface ITradingStrategyService
{
    Task<IReadOnlyList<TradingStrategy>> GetAllByUserIdAsync(int userId);
    Task<TradingStrategy?>               GetByIdAsync(int strategyId);
    Task<TradingStrategy>                CreateAsync(int userId, string title, string? description,
                                                      byte[]? imageData, string? imageMimeType,
                                                      IEnumerable<string> rules,
                                                      IEnumerable<string> confluences);
    Task<TradingStrategy>                UpdateAsync(int strategyId, string title, string? description,
                                                      byte[]? imageData, string? imageMimeType,
                                                      IEnumerable<string> rules,
                                                      IEnumerable<string> confluences);
    Task                                 DeleteAsync(int strategyId);
    Task<TradingStrategy>                RateStrategyAsync(int strategyId, IDictionary<int, int?> confluenceRatings);
}
