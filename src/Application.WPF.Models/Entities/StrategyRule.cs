namespace Application.WPF.Models.Entities;

public class StrategyRule
{
    public int      Id          { get; set; }
    public int      StrategyId  { get; set; }
    public string   Description { get; set; } = string.Empty;
    public int      OrderIndex  { get; set; }
    public DateTime CreatedAt   { get; set; }

    public TradingStrategy Strategy { get; set; } = null!;
}
