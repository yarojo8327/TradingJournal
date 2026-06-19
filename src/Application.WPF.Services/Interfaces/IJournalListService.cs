using Application.WPF.Models.Entities;

namespace Application.WPF.Services.Interfaces;

public interface IJournalListService
{
    Task<IReadOnlyList<string>> GetNamesAsync(int userId, string category);
    Task<IReadOnlyList<JournalListItem>> GetItemsAsync(int userId, string category);
    Task<JournalListItem> CreateAsync(int userId, string category, string name);
    Task DeleteAsync(int id);
    Task EnsureDefaultsAsync(int userId);
}

public static class JournalListCategory
{
    public const string EmotionalState = "EmotionalState";
    public const string MistakeType    = "MistakeType";
}
