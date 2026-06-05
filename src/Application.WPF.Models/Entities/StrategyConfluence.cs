namespace Application.WPF.Models.Entities;

public class StrategyConfluence
{
    public int      Id         { get; set; }
    public int      StrategyId { get; set; }
    public string   Name       { get; set; } = string.Empty;
    public int      OrderIndex { get; set; }
    public int?     Rating     { get; set; }  // 1-10, null = sin calificar
    public DateTime CreatedAt  { get; set; }

    public TradingStrategy Strategy { get; set; } = null!;
}
