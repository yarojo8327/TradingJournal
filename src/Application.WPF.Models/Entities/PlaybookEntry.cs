namespace Application.WPF.Models.Entities;

public class PlaybookEntry
{
    public int       Id            { get; set; }
    public int       UserId        { get; set; }
    public int?      StrategyId    { get; set; }
    public string    Symbol        { get; set; } = string.Empty;
    public string?   Notes         { get; set; }
    public byte[]?   ImageData     { get; set; }
    public string?   ImageMimeType { get; set; }
    public double?   Rating        { get; set; }   // promedio ponderado de confluencias
    public int?      ManualRating  { get; set; }   // calificación manual 1-10 (estrellas)
    public DateTime  CreatedAt     { get; set; }
    public DateTime? UpdatedAt     { get; set; }

    public bool HasImage => ImageData is { Length: > 0 };

    // Navegación
    public User             User              { get; set; } = null!;
    public TradingStrategy? Strategy          { get; set; }
    public ICollection<PlaybookConfluenceRating> ConfluenceRatings { get; set; } = new List<PlaybookConfluenceRating>();
}
