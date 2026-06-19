using Application.WPF.Models.Entities;

namespace Application.WPF.Services.Interfaces;

/// <summary>Input parameters for a lot-size calculation (HU-TRD-001).</summary>
public record LotCalculationRequest(
    int      UserId,
    int?     AccountId,
    string   Symbol,
    decimal  Capital,
    decimal  RiskPercent,
    decimal  EntryPrice,
    decimal  StopLoss,
    decimal? TakeProfit,
    string   AccountCurrency,
    decimal? MaxRiskPercentPerTrade);

/// <summary>Result of a lot-size calculation. <see cref="Success"/> is false when a business rule blocks the calculation.</summary>
public record LotCalculationResult(
    bool     Success,
    decimal? LotSize,
    decimal  RiskAmount,
    decimal? RiskRewardRatio,
    string?  ErrorMessage,
    string?  WarningMessage);

public interface ILotCalculatorService
{
    /// <summary>
    /// Calculates the recommended lot size from capital, risk %, and Stop Loss distance (RN-001, RN-002, RN-004, RN-005).
    /// Does not persist anything — call <see cref="SaveAsync"/> separately to audit (RN-006).
    /// </summary>
    Task<LotCalculationResult> CalculateAsync(LotCalculationRequest request);

    /// <summary>Persists a successful calculation for audit/analysis (RN-006).</summary>
    Task<LotCalculation> SaveAsync(LotCalculationRequest request, LotCalculationResult result);

    Task<IReadOnlyList<LotCalculation>> GetHistoryAsync(int userId, int take = 50);
}
