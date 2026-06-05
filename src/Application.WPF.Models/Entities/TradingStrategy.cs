namespace Application.WPF.Models.Entities;

public class TradingStrategy
{
    public int       Id            { get; set; }
    public int       UserId        { get; set; }
    public string    Title         { get; set; } = string.Empty;
    public string?   Description   { get; set; }
    public byte[]?   ImageData     { get; set; }
    public string?   ImageMimeType { get; set; }
    public DateTime  CreatedAt     { get; set; }
    public DateTime? UpdatedAt     { get; set; }

    public bool HasImage => ImageData != null && ImageData.Length > 0;

    /// <summary>Promedio de confluencias calificadas (1-10). Null si ninguna está calificada.</summary>
    public double? AverageRating =>
        Confluences.Any(c => c.Rating.HasValue)
            ? Math.Round(Confluences.Where(c => c.Rating.HasValue)
                                    .Average(c => (double)c.Rating!.Value), 1)
            : null;

    public bool HasAverageRating => AverageRating.HasValue;

    /// <summary>Un setup se considera de calidad si el promedio supera 6.5.</summary>
    public bool IsQualifiedSetup => AverageRating.HasValue && AverageRating.Value >= 6.5;

    public User                           User         { get; set; } = null!;
    public ICollection<StrategyRule>      Rules        { get; set; } = new List<StrategyRule>();
    public ICollection<StrategyConfluence> Confluences  { get; set; } = new List<StrategyConfluence>();
}
