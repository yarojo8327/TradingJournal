using Application.WPF.Models.Entities;
using Application.WPF.Models.Enums;


namespace Application.WPF.Services.Interfaces;

public interface ITradeService
{
    Task<IReadOnlyList<TradeEntry>> GetAllByAccountIdAsync(int accountId);
    Task<IReadOnlyList<TradeEntry>> GetAllByUserIdAsync(int userId);
    Task<TradeEntry?> GetByIdAsync(int tradeId);
    Task<TradeEntry> CreateAsync(TradeEntryData data);
    Task<TradeEntry> UpdateAsync(int tradeId, TradeEntryData data);
    Task DeleteAsync(int tradeId);
}

public record TradeEntryData(
    int            AccountId,
    int?           StrategyId,
    string         Symbol,
    TradeDirection Direction,
    DateTime       EntryDate,
    DateTime?      ExitDate,
    decimal        EntryPrice,
    decimal?       ExitPrice,
    decimal?       StopLoss,
    decimal?       TakeProfit,
    decimal?       PositionSizeLots,
    decimal?       RiskAmount,
    decimal?       ProfitLoss,
    decimal?       PipsResult,
    decimal?       RiskRewardRatio,
    TradeResult    Result,
    TradingSession? Session,
    string?        Timeframe,
    TradingType?   TradingType,
    int?           SetupQuality,
    int?           ConfluencesCount,
    bool           IsFalseBreakout,
    int?           Rating,
    string?        EmotionalState,
    string?        MistakeType,
    string?        Notes,
    string?        ScreenshotUrl
);
