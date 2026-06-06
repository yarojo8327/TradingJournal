using Application.WPF.Models.Entities;

namespace Application.WPF.Services.Interfaces;

public interface IPlaybookService
{
    Task<IReadOnlyList<PlaybookEntry>> GetAllByUserIdAsync(int userId);
    Task<PlaybookEntry>                CreateAsync(PlaybookEntryData data);
    Task<PlaybookEntry>                UpdateAsync(int id, PlaybookEntryData data);
    Task                               DeleteAsync(int id);
}

public record PlaybookEntryData(
    int                                        UserId,
    int?                                       StrategyId,
    string                                     Symbol,
    string?                                    Notes,
    byte[]?                                    ImageData,
    string?                                    ImageMimeType,
    double?                                    Rating,
    int?                                       ManualRating,
    IReadOnlyList<PlaybookConfluenceRatingData> ConfluenceRatings);

public record PlaybookConfluenceRatingData(
    int    ConfluenceId,
    string ConfluenceName,
    int    OrderIndex,
    int    Rating);
