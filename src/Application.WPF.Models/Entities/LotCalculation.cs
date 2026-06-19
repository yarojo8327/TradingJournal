namespace Application.WPF.Models.Entities;

/// <summary>Audit record of a lot-size calculation (RN-006).</summary>
public class LotCalculation
{
    public int      Id                { get; set; }
    public int      UserId            { get; set; }
    public int?     AccountId         { get; set; }
    public string   Symbol            { get; set; } = string.Empty;
    public decimal  Capital           { get; set; }
    public decimal  RiskPercent       { get; set; }
    public decimal  EntryPrice        { get; set; }
    public decimal  StopLoss          { get; set; }
    public decimal? TakeProfit        { get; set; }
    public decimal  RiskAmount        { get; set; }
    public decimal  LotSize           { get; set; }
    public decimal? RiskRewardRatio   { get; set; }
    public string   AccountCurrency   { get; set; } = string.Empty;
    public DateTime CreatedAt         { get; set; }

    public User           User    { get; set; } = null!;
    public TradingAccount? Account { get; set; }
}
