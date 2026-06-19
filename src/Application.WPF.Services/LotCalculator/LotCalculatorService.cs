using Application.WPF.Infrastructure.Data;
using Application.WPF.Models.Entities;
using Application.WPF.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Application.WPF.Services.LotCalculator;

public class LotCalculatorService : ILotCalculatorService
{
    private readonly TradingJournalDbContext _db;
    private readonly ISymbolMappingService   _symbolMappingService;

    public LotCalculatorService(TradingJournalDbContext db, ISymbolMappingService symbolMappingService)
    {
        _db                   = db;
        _symbolMappingService = symbolMappingService;
    }

    public async Task<LotCalculationResult> CalculateAsync(LotCalculationRequest request)
    {
        // RN-001: Stop Loss or Entry Price cannot be zero, and they cannot be equal (zero distance).
        if (request.EntryPrice == 0 || request.StopLoss == 0)
            return new LotCalculationResult(false, null, 0, null,
                "El precio de entrada y el Stop Loss son obligatorios y deben ser distintos de cero.", null);

        var priceDistance = Math.Abs(request.EntryPrice - request.StopLoss);
        if (priceDistance == 0)
            return new LotCalculationResult(false, null, 0, null,
                "El Stop Loss no puede ser igual al precio de entrada.", null);

        if (request.Capital <= 0 || request.RiskPercent <= 0)
            return new LotCalculationResult(false, null, 0, null,
                "El capital y el porcentaje de riesgo deben ser mayores que cero.", null);

        // Scenario 4: the instrument must have a configured point value.
        var valuePerPoint = await _symbolMappingService.GetValuePerPointAsync(request.Symbol);
        if (valuePerPoint is null || valuePerPoint <= 0)
            return new LotCalculationResult(false, null, 0, null,
                $"El activo \"{request.Symbol}\" no tiene configurado un valor por punto. Configúrelo en Configuración > Símbolos.", null);

        var riskAmount = request.Capital * (request.RiskPercent / 100m);
        var lotSize    = Math.Round(riskAmount / (priceDistance * valuePerPoint.Value), 2);

        decimal? riskReward = null;
        if (request.TakeProfit.HasValue && request.TakeProfit.Value != 0)
        {
            var rewardDistance = Math.Abs(request.TakeProfit.Value - request.EntryPrice);
            riskReward = Math.Round(rewardDistance / priceDistance, 2);
        }

        // RN-002: warn (not block) when the requested risk exceeds the account's configured maximum.
        string? warning = null;
        if (request.MaxRiskPercentPerTrade.HasValue && request.RiskPercent > request.MaxRiskPercentPerTrade.Value)
            warning = $"El riesgo ingresado ({request.RiskPercent:0.##}%) supera el máximo configurado para esta cuenta ({request.MaxRiskPercentPerTrade.Value:0.##}%).";

        return new LotCalculationResult(true, lotSize, riskAmount, riskReward, null, warning);
    }

    public async Task<LotCalculation> SaveAsync(LotCalculationRequest request, LotCalculationResult result)
    {
        if (!result.Success || result.LotSize is null)
            throw new InvalidOperationException("Solo se pueden guardar cálculos exitosos.");

        var entry = new LotCalculation
        {
            UserId          = request.UserId,
            AccountId       = request.AccountId,
            Symbol          = request.Symbol.Trim().ToUpper(),
            Capital         = request.Capital,
            RiskPercent     = request.RiskPercent,
            EntryPrice      = request.EntryPrice,
            StopLoss        = request.StopLoss,
            TakeProfit      = request.TakeProfit,
            RiskAmount      = result.RiskAmount,
            LotSize         = result.LotSize.Value,
            RiskRewardRatio = result.RiskRewardRatio,
            AccountCurrency = request.AccountCurrency,
            CreatedAt       = DateTime.UtcNow
        };

        _db.LotCalculations.Add(entry);
        await _db.SaveChangesAsync();
        return entry;
    }

    public async Task<IReadOnlyList<LotCalculation>> GetHistoryAsync(int userId, int take = 50) =>
        await _db.LotCalculations
                 .Where(c => c.UserId == userId)
                 .OrderByDescending(c => c.CreatedAt)
                 .Take(take)
                 .ToListAsync();
}
