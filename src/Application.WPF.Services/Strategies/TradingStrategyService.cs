using Application.WPF.Infrastructure.Data;
using Application.WPF.Models.Entities;
using Application.WPF.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.WPF.Services.Strategies;

public class TradingStrategyService : ITradingStrategyService
{
    private readonly TradingJournalDbContext        _db;
    private readonly ILogger<TradingStrategyService> _logger;

    public TradingStrategyService(TradingJournalDbContext db, ILogger<TradingStrategyService> logger)
    {
        _db     = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TradingStrategy>> GetAllByUserIdAsync(int userId) =>
        await _db.TradingStrategies
                 .Include(s => s.Rules.OrderBy(r => r.OrderIndex))
                 .Where(s => s.UserId == userId)
                 .OrderByDescending(s => s.CreatedAt)
                 .ToListAsync();

    public async Task<TradingStrategy?> GetByIdAsync(int strategyId) =>
        await _db.TradingStrategies
                 .Include(s => s.Rules.OrderBy(r => r.OrderIndex))
                 .FirstOrDefaultAsync(s => s.Id == strategyId);

    public async Task<TradingStrategy> CreateAsync(
        int userId, string title, string? description,
        byte[]? imageData, string? imageMimeType,
        IEnumerable<string> rules)
    {
        var strategy = new TradingStrategy
        {
            UserId        = userId,
            Title         = title.Trim(),
            Description   = description?.Trim(),
            ImageData     = imageData,
            ImageMimeType = imageMimeType,
            CreatedAt     = DateTime.UtcNow
        };

        var ruleList = rules.Where(r => !string.IsNullOrWhiteSpace(r)).ToList();
        for (int i = 0; i < ruleList.Count; i++)
        {
            strategy.Rules.Add(new StrategyRule
            {
                Description = ruleList[i].Trim(),
                OrderIndex  = i,
                CreatedAt   = DateTime.UtcNow
            });
        }

        _db.TradingStrategies.Add(strategy);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Strategy '{Title}' created for user {UserId}", strategy.Title, userId);
        return strategy;
    }

    public async Task<TradingStrategy> UpdateAsync(
        int strategyId, string title, string? description,
        byte[]? imageData, string? imageMimeType,
        IEnumerable<string> rules)
    {
        var strategy = await _db.TradingStrategies
                                .Include(s => s.Rules)
                                .FirstOrDefaultAsync(s => s.Id == strategyId)
            ?? throw new InvalidOperationException("Estrategia no encontrada.");

        strategy.Title         = title.Trim();
        strategy.Description   = description?.Trim();
        strategy.ImageData     = imageData;
        strategy.ImageMimeType = imageMimeType;
        strategy.UpdatedAt     = DateTime.UtcNow;

        // Reemplazar todas las reglas
        _db.StrategyRules.RemoveRange(strategy.Rules);
        strategy.Rules.Clear();

        var ruleList = rules.Where(r => !string.IsNullOrWhiteSpace(r)).ToList();
        for (int i = 0; i < ruleList.Count; i++)
        {
            strategy.Rules.Add(new StrategyRule
            {
                Description = ruleList[i].Trim(),
                OrderIndex  = i,
                CreatedAt   = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("Strategy {Id} updated", strategyId);
        return strategy;
    }

    public async Task DeleteAsync(int strategyId)
    {
        var strategy = await _db.TradingStrategies.FindAsync(strategyId)
            ?? throw new InvalidOperationException("Estrategia no encontrada.");

        _db.TradingStrategies.Remove(strategy);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Strategy {Id} deleted", strategyId);
    }
}
