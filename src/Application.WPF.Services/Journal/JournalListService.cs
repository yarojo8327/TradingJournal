using Application.WPF.Infrastructure.Data;
using Application.WPF.Models.Entities;
using Application.WPF.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Application.WPF.Services.Journal;

public class JournalListService : IJournalListService
{
    private readonly TradingJournalDbContext _db;

    public JournalListService(TradingJournalDbContext db) => _db = db;

    public async Task<IReadOnlyList<string>> GetNamesAsync(int userId, string category) =>
        await _db.JournalListItems
                 .Where(j => j.UserId == userId && j.Category == category)
                 .OrderBy(j => j.SortOrder).ThenBy(j => j.Name)
                 .Select(j => j.Name)
                 .ToListAsync();

    public async Task<IReadOnlyList<JournalListItem>> GetItemsAsync(int userId, string category) =>
        await _db.JournalListItems
                 .Where(j => j.UserId == userId && j.Category == category)
                 .OrderBy(j => j.SortOrder).ThenBy(j => j.Name)
                 .ToListAsync();

    public async Task<JournalListItem> CreateAsync(int userId, string category, string name)
    {
        var maxOrder = await _db.JournalListItems
                                .Where(j => j.UserId == userId && j.Category == category)
                                .MaxAsync(j => (int?)j.SortOrder) ?? 0;
        var item = new JournalListItem
        {
            UserId    = userId,
            Category  = category,
            Name      = name.Trim(),
            SortOrder = maxOrder + 1
        };
        _db.JournalListItems.Add(item);
        await _db.SaveChangesAsync();
        return item;
    }

    public async Task DeleteAsync(int id)
    {
        var item = await _db.JournalListItems.FindAsync(id);
        if (item is null) return;
        _db.JournalListItems.Remove(item);
        await _db.SaveChangesAsync();
    }

    public async Task EnsureDefaultsAsync(int userId)
    {
        var hasEmotional = await _db.JournalListItems
                                    .AnyAsync(j => j.UserId == userId && j.Category == JournalListCategory.EmotionalState);
        if (!hasEmotional)
        {
            string[] emotional = { "Calmado", "Disciplinado", "Confiado", "Emocionado", "Ansioso", "Temeroso", "FOMO", "Venganza" };
            for (int i = 0; i < emotional.Length; i++)
                _db.JournalListItems.Add(new JournalListItem { UserId = userId, Category = JournalListCategory.EmotionalState, Name = emotional[i], SortOrder = i });
        }

        var hasMistake = await _db.JournalListItems
                                  .AnyAsync(j => j.UserId == userId && j.Category == JournalListCategory.MistakeType);
        if (!hasMistake)
        {
            string[] mistakes =
            {
                "FOMO — Entré tarde", "Revenge trading", "Tamaño excesivo de posición",
                "Ignoré la invalidación", "Salí demasiado pronto", "No respeté el SL",
                "Operé contra la tendencia", "Setup de baja calidad", "Gestión de riesgo incorrecta", "Otro"
            };
            for (int i = 0; i < mistakes.Length; i++)
                _db.JournalListItems.Add(new JournalListItem { UserId = userId, Category = JournalListCategory.MistakeType, Name = mistakes[i], SortOrder = i });
        }

        await _db.SaveChangesAsync();
    }
}
