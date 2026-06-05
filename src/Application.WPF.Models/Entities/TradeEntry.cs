using Application.WPF.Models.Enums;

namespace Application.WPF.Models.Entities;

public class TradeEntry
{
    public int             Id               { get; set; }
    public int             AccountId        { get; set; }
    public int?            StrategyId       { get; set; }

    // Instrumento y dirección
    public string          Symbol           { get; set; } = string.Empty;
    public TradeDirection  Direction        { get; set; }

    // Timing
    public DateTime        EntryDate        { get; set; }
    public DateTime?       ExitDate         { get; set; }

    // Precios
    public decimal         EntryPrice       { get; set; }
    public decimal?        ExitPrice        { get; set; }
    public decimal?        StopLoss         { get; set; }
    public decimal?        TakeProfit       { get; set; }

    // Tamaño y riesgo
    public decimal?        PositionSizeLots { get; set; }
    public decimal?        RiskAmount       { get; set; }

    // Resultado
    public decimal?        ProfitLoss       { get; set; }
    public decimal?        PipsResult       { get; set; }
    public decimal?        RiskRewardRatio  { get; set; }
    public TradeResult     Result           { get; set; } = TradeResult.Open;

    // Contexto de mercado
    public TradingSession? Session          { get; set; }
    public string?         Timeframe        { get; set; }

    // Calidad del setup
    public int?            SetupQuality     { get; set; }
    public int?            ConfluencesCount { get; set; }
    public bool            IsFalseBreakout  { get; set; }

    // Psicología
    public EmotionalState? EmotionalState   { get; set; }
    public string?         MistakeType      { get; set; }

    // Notas y evidencia
    public string?         Notes            { get; set; }
    public string?         ScreenshotUrl    { get; set; }

    public DateTime        CreatedAt        { get; set; }
    public DateTime?       UpdatedAt        { get; set; }

    // Navegación
    public TradingAccount       Account  { get; set; } = null!;
    public TradingStrategy?     Strategy { get; set; }
}
