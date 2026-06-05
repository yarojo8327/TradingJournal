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

    private IQueryable<TradingStrategy> WithRelations() =>
        _db.TradingStrategies
           .Include(s => s.Rules.OrderBy(r => r.OrderIndex))
           .Include(s => s.Confluences.OrderBy(c => c.OrderIndex));

    public async Task<IReadOnlyList<TradingStrategy>> GetAllByUserIdAsync(int userId) =>
        await WithRelations()
              .Where(s => s.UserId == userId)
              .OrderByDescending(s => s.CreatedAt)
              .ToListAsync();

    public async Task<TradingStrategy?> GetByIdAsync(int strategyId) =>
        await WithRelations().FirstOrDefaultAsync(s => s.Id == strategyId);

    public async Task<TradingStrategy> CreateAsync(
        int userId, string title, string? description,
        byte[]? imageData, string? imageMimeType,
        IEnumerable<string> rules,
        IEnumerable<string> confluences)
    {
        var strategy = new TradingStrategy
        {
            UserId        = userId,
            Title         = title.Trim(),
            Description   = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            ImageData     = imageData,
            ImageMimeType = imageMimeType,
            CreatedAt     = DateTime.UtcNow
        };

        var ruleList = rules.Where(r => !string.IsNullOrWhiteSpace(r)).ToList();
        for (int i = 0; i < ruleList.Count; i++)
            strategy.Rules.Add(new StrategyRule
            {
                Description = ruleList[i].Trim(),
                OrderIndex  = i,
                CreatedAt   = DateTime.UtcNow
            });

        var conflList = confluences.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
        for (int i = 0; i < conflList.Count; i++)
            strategy.Confluences.Add(new StrategyConfluence
            {
                Name       = conflList[i].Trim(),
                OrderIndex = i,
                CreatedAt  = DateTime.UtcNow
            });

        _db.TradingStrategies.Add(strategy);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Strategy '{Title}' created for user {UserId}", strategy.Title, userId);
        return strategy;
    }

    public async Task<TradingStrategy> UpdateAsync(
        int strategyId, string title, string? description,
        byte[]? imageData, string? imageMimeType,
        IEnumerable<string> rules,
        IEnumerable<string> confluences)
    {
        var strategy = await _db.TradingStrategies.FindAsync(strategyId)
            ?? throw new InvalidOperationException("Estrategia no encontrada.");

        strategy.Title         = title.Trim();
        strategy.Description   = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        strategy.ImageData     = imageData;
        strategy.ImageMimeType = imageMimeType;
        strategy.UpdatedAt     = DateTime.UtcNow;

        // Reglas: eliminar y recrear via SQL directo para evitar FK constraint
        await _db.Database.ExecuteSqlRawAsync(
            @"DELETE FROM ""StrategyRules"" WHERE ""StrategyId"" = {0}", strategyId);

        var ruleList = rules.Where(r => !string.IsNullOrWhiteSpace(r)).ToList();
        for (int i = 0; i < ruleList.Count; i++)
            _db.StrategyRules.Add(new StrategyRule
            {
                StrategyId  = strategyId,
                Description = ruleList[i].Trim(),
                OrderIndex  = i,
                CreatedAt   = DateTime.UtcNow
            });

        // Confluencias: preservar Rating al recrear — buscar por nombre en las existentes
        var existingConfluences = await _db.StrategyConfluences
            .Where(c => c.StrategyId == strategyId)
            .ToListAsync();

        await _db.Database.ExecuteSqlRawAsync(
            @"DELETE FROM ""StrategyConfluences"" WHERE ""StrategyId"" = {0}", strategyId);

        var conflList = confluences.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
        for (int i = 0; i < conflList.Count; i++)
        {
            var name   = conflList[i].Trim();
            var oldRating = existingConfluences
                .FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                ?.Rating;

            _db.StrategyConfluences.Add(new StrategyConfluence
            {
                StrategyId = strategyId,
                Name       = name,
                OrderIndex = i,
                Rating     = oldRating,
                CreatedAt  = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        _logger.LogInformation("Strategy {Id} updated", strategyId);
        return await WithRelations().FirstAsync(s => s.Id == strategyId);
    }

    public async Task DeleteAsync(int strategyId)
    {
        var strategy = await _db.TradingStrategies.FindAsync(strategyId)
            ?? throw new InvalidOperationException("Estrategia no encontrada.");

        _db.TradingStrategies.Remove(strategy);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Strategy {Id} deleted", strategyId);
    }

    public async Task<TradingStrategy> RateStrategyAsync(
        int strategyId, IDictionary<int, int?> confluenceRatings)
    {
        var confluences = await _db.StrategyConfluences
            .Where(c => c.StrategyId == strategyId)
            .ToListAsync();

        foreach (var confluence in confluences)
        {
            if (confluenceRatings.TryGetValue(confluence.Id, out var rating))
                confluence.Rating = rating is >= 1 and <= 10 ? rating : null;
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("Strategy {Id} rated", strategyId);

        return await WithRelations().FirstAsync(s => s.Id == strategyId);
    }
}
