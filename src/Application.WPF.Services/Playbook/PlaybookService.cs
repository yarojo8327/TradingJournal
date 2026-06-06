using Application.WPF.Infrastructure.Data;
using Application.WPF.Models.Entities;
using Application.WPF.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.WPF.Services.Playbook;

public class PlaybookService : IPlaybookService
{
    private readonly TradingJournalDbContext _db;
    private readonly ILogger<PlaybookService> _logger;

    public PlaybookService(TradingJournalDbContext db, ILogger<PlaybookService> logger)
    {
        _db     = db;
        _logger = logger;
    }

    private IQueryable<PlaybookEntry> WithRelations() =>
        _db.PlaybookEntries
           .Include(e => e.Strategy)
           .Include(e => e.ConfluenceRatings.OrderBy(r => r.OrderIndex));

    public async Task<IReadOnlyList<PlaybookEntry>> GetAllByUserIdAsync(int userId) =>
        await WithRelations()
              .Where(e => e.UserId == userId)
              .OrderByDescending(e => e.CreatedAt)
              .ToListAsync();

    public async Task<PlaybookEntry> CreateAsync(PlaybookEntryData data)
    {
        var entry = new PlaybookEntry
        {
            UserId        = data.UserId,
            StrategyId    = data.StrategyId,
            Symbol        = data.Symbol.Trim(),
            Notes         = string.IsNullOrWhiteSpace(data.Notes) ? null : data.Notes.Trim(),
            ImageData     = data.ImageData,
            ImageMimeType = data.ImageMimeType,
            Rating        = data.Rating,
            ManualRating  = data.ManualRating,
            CreatedAt     = DateTime.UtcNow,
            ConfluenceRatings = data.ConfluenceRatings
                .Select(r => new PlaybookConfluenceRating
                {
                    ConfluenceId   = r.ConfluenceId,
                    ConfluenceName = r.ConfluenceName,
                    OrderIndex     = r.OrderIndex,
                    Rating         = r.Rating
                })
                .ToList()
        };

        _db.PlaybookEntries.Add(entry);
        await _db.SaveChangesAsync();

        _logger.LogInformation("PlaybookEntry created: {Id} - {Symbol}", entry.Id, entry.Symbol);
        return entry;
    }

    public async Task<PlaybookEntry> UpdateAsync(int id, PlaybookEntryData data)
    {
        var entry = await WithRelations().FirstOrDefaultAsync(e => e.Id == id)
            ?? throw new InvalidOperationException($"PlaybookEntry {id} not found");

        entry.StrategyId    = data.StrategyId;
        entry.Symbol        = data.Symbol.Trim();
        entry.Notes         = string.IsNullOrWhiteSpace(data.Notes) ? null : data.Notes.Trim();
        entry.ImageData     = data.ImageData;
        entry.ImageMimeType = data.ImageMimeType;
        entry.Rating        = data.Rating;
        entry.ManualRating  = data.ManualRating;
        entry.UpdatedAt     = DateTime.UtcNow;

        // Reemplazar confluencias con ExecuteSqlRawAsync + ChangeTracker.Clear()
        await _db.Database.ExecuteSqlRawAsync(
            @"DELETE FROM ""PlaybookConfluenceRatings"" WHERE ""PlaybookEntryId"" = {0}", id);
        _db.ChangeTracker.Clear();

        // Re-adjuntar entry y agregar nuevas calificaciones
        entry = await WithRelations().FirstOrDefaultAsync(e => e.Id == id)
            ?? throw new InvalidOperationException($"PlaybookEntry {id} not found after clear");

        entry.StrategyId    = data.StrategyId;
        entry.Symbol        = data.Symbol.Trim();
        entry.Notes         = string.IsNullOrWhiteSpace(data.Notes) ? null : data.Notes.Trim();
        entry.ImageData     = data.ImageData;
        entry.ImageMimeType = data.ImageMimeType;
        entry.Rating        = data.Rating;
        entry.ManualRating  = data.ManualRating;
        entry.UpdatedAt     = DateTime.UtcNow;

        foreach (var r in data.ConfluenceRatings)
        {
            _db.Set<PlaybookConfluenceRating>().Add(new PlaybookConfluenceRating
            {
                PlaybookEntryId = id,
                ConfluenceId    = r.ConfluenceId,
                ConfluenceName  = r.ConfluenceName,
                OrderIndex      = r.OrderIndex,
                Rating          = r.Rating
            });
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("PlaybookEntry updated: {Id}", id);
        return entry;
    }

    public async Task DeleteAsync(int id)
    {
        var entry = await _db.PlaybookEntries.FindAsync(id);
        if (entry is null) return;
        _db.PlaybookEntries.Remove(entry);
        await _db.SaveChangesAsync();
        _logger.LogInformation("PlaybookEntry deleted: {Id}", id);
    }
}
