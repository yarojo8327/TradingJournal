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

    public User                  User  { get; set; } = null!;
    public ICollection<StrategyRule> Rules { get; set; } = new List<StrategyRule>();
}
