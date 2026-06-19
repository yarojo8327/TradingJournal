using Application.WPF.Models.Enums;

namespace Application.WPF.Models.Entities;

public class TradingAccount
{
    public int         Id             { get; set; }
    public int         UserId         { get; set; }
    public string      Broker         { get; set; } = string.Empty;
    public string      AccountNumber  { get; set; } = string.Empty;
    public AccountType AccountType    { get; set; }
    public decimal     InitialCapital { get; set; }
    public string      BaseCurrency   { get; set; } = string.Empty;
    public string      Leverage       { get; set; } = string.Empty;
    public bool        IsCentAccount  { get; set; }
    /// <summary>Maximum risk % per trade configured by the user (HU-TRD-001 RN-002). Default 2%.</summary>
    public decimal     MaxRiskPercentPerTrade { get; set; } = 2.0m;
    public DateTime    StartDate      { get; set; }
    public DateTime    CreatedAt      { get; set; }
    public DateTime?   UpdatedAt      { get; set; }

    public User User { get; set; } = null!;

    public override string ToString() => $"{Broker}  —  {AccountNumber}";
}
