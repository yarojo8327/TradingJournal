using Application.WPF.Infrastructure.Data;
using Application.WPF.Models.Entities;
using Application.WPF.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.WPF.Services.Trades;

public class TradeService : ITradeService
{
    private readonly TradingJournalDbContext _db;
    private readonly ILogger<TradeService>  _logger;

    public TradeService(TradingJournalDbContext db, ILogger<TradeService> logger)
    {
        _db     = db;
        _logger = logger;
    }

    private IQueryable<TradeEntry> WithRelations() =>
        _db.TradeEntries
           .Include(t => t.Account)
           .Include(t => t.Strategy);

    public async Task<IReadOnlyList<TradeEntry>> GetAllByAccountIdAsync(int accountId) =>
        await WithRelations()
              .Where(t => t.AccountId == accountId)
              .OrderByDescending(t => t.EntryDate)
              .ToListAsync();

    public async Task<IReadOnlyList<TradeEntry>> GetAllByUserIdAsync(int userId) =>
        await WithRelations()
              .Where(t => t.Account.UserId == userId)
              .OrderByDescending(t => t.EntryDate)
              .ToListAsync();

    public async Task<TradeEntry?> GetByIdAsync(int tradeId) =>
        await WithRelations().FirstOrDefaultAsync(t => t.Id == tradeId);

    public async Task<TradeEntry> CreateAsync(TradeEntryData d)
    {
        var entry = MapToEntity(new TradeEntry { CreatedAt = DateTime.UtcNow }, d);
        _db.TradeEntries.Add(entry);
        await _db.SaveChangesAsync();
        _logger.LogInformation("TradeEntry created for account {AccountId}", d.AccountId);
        return (await GetByIdAsync(entry.Id))!;
    }

    public async Task<TradeEntry> UpdateAsync(int tradeId, TradeEntryData d)
    {
        var entry = await _db.TradeEntries.FindAsync(tradeId)
            ?? throw new InvalidOperationException("Trade no encontrado.");

        entry.UpdatedAt = DateTime.UtcNow;
        MapToEntity(entry, d);
        await _db.SaveChangesAsync();
        _logger.LogInformation("TradeEntry {Id} updated", tradeId);
        return (await GetByIdAsync(tradeId))!;
    }

    public async Task DeleteAsync(int tradeId)
    {
        var entry = await _db.TradeEntries.FindAsync(tradeId)
            ?? throw new InvalidOperationException("Trade no encontrado.");

        _db.TradeEntries.Remove(entry);
        await _db.SaveChangesAsync();
        _logger.LogInformation("TradeEntry {Id} deleted", tradeId);
    }

    private static TradeEntry MapToEntity(TradeEntry e, TradeEntryData d)
    {
        e.AccountId        = d.AccountId;
        e.StrategyId       = d.StrategyId;
        e.Symbol           = d.Symbol.Trim().ToUpperInvariant();
        e.Direction        = d.Direction;
        e.EntryDate        = d.EntryDate;
        e.ExitDate         = d.ExitDate;
        e.EntryPrice       = d.EntryPrice;
        e.ExitPrice        = d.ExitPrice;
        e.StopLoss         = d.StopLoss;
        e.TakeProfit       = d.TakeProfit;
        e.PositionSizeLots = d.PositionSizeLots;
        e.RiskAmount       = d.RiskAmount;
        e.ProfitLoss       = d.ProfitLoss;
        e.PipsResult       = d.PipsResult;
        e.RiskRewardRatio  = d.RiskRewardRatio;
        e.Result           = d.Result;
        e.Session          = d.Session;
        e.Timeframe        = d.Timeframe;
        e.TradingType      = d.TradingType;
        e.SetupQuality     = d.SetupQuality;
        e.ConfluencesCount = d.ConfluencesCount;
        e.IsFalseBreakout  = d.IsFalseBreakout;
        e.Rating           = d.Rating is >= 1 and <= 10 ? d.Rating : null;
        e.EmotionalState   = string.IsNullOrWhiteSpace(d.EmotionalState) ? null : d.EmotionalState.Trim();
        e.MistakeType      = string.IsNullOrWhiteSpace(d.MistakeType) ? null : d.MistakeType.Trim();
        e.Notes            = string.IsNullOrWhiteSpace(d.Notes) ? null : d.Notes.Trim();
        e.ScreenshotUrl    = string.IsNullOrWhiteSpace(d.ScreenshotUrl) ? null : d.ScreenshotUrl.Trim();
        return e;
    }
}
