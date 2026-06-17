using Application.WPF.Models.Entities;
using Application.WPF.Models.Enums;
using System;

using System.Windows;
using System.Windows.Input;

namespace Application.WPF.Views.Journal;

public partial class TradeDetailWindow : Window
{
    private readonly TradeEntry _trade;

    public TradeDetailWindow(TradeEntry trade)
    {
        _trade = trade;
        InitializeComponent();
        PopulateFields();
    }

    private void PopulateFields()
    {
        TitleText.Text  = $"{_trade.Symbol}  —  {_trade.EntryDate:dd/MM/yyyy}";
        SymbolBadge.Text = _trade.Symbol;

        AccountText.Text  = _trade.Account?.ToString() ?? "—";
        StrategyText.Text = _trade.Strategy?.Title ?? "—";
        DirectionText.Text = _trade.Direction == TradeDirection.Long ? "Long ▲" : "Short ▼";

        EntryDateText.Text = _trade.EntryDate.ToString("dd/MM/yyyy  HH:mm");
        ExitDateText.Text  = _trade.ExitDate.HasValue
            ? _trade.ExitDate.Value.ToString("dd/MM/yyyy  HH:mm")
            : "—";

        EntryPriceText.Text   = _trade.EntryPrice.ToString("G");
        ExitPriceText.Text    = _trade.ExitPrice?.ToString("G")   ?? "—";
        StopLossText.Text     = _trade.StopLoss?.ToString("G")    ?? "—";
        TakeProfitText.Text   = _trade.TakeProfit?.ToString("G")  ?? "—";
        PositionSizeText.Text = _trade.PositionSizeLots?.ToString("G") ?? "—";

        ResultText.Text = _trade.Result switch
        {
            TradeResult.Profit    => "✓ Ganancia",
            TradeResult.Loss      => "✗ Pérdida",
            TradeResult.BreakEven => "≈ BreakEven",
            _                    => "● Abierto"
        };

        if (_trade.ProfitLoss.HasValue)
        {
            var pnl = _trade.ProfitLoss.Value;
            PnlText.Text = pnl >= 0 ? $"+ $ {pnl:N2}" : $"- $ {Math.Abs(pnl):N2}";
            PnlText.Foreground = pnl >= 0
                ? (System.Windows.Media.Brush)FindResource("AccentGreenBrush")
                : (System.Windows.Media.Brush)FindResource("AccentRedBrush");
        }
        else
        {
            PnlText.Text = "—";
        }

        RiskText.Text = _trade.RiskAmount?.ToString("N2") is string r ? $"$ {r}" : "—";
        RrText.Text   = _trade.RiskRewardRatio.HasValue ? $"{_trade.RiskRewardRatio.Value:F2}" : "—";

        SessionText.Text = _trade.Session switch
        {
            TradingSession.Asian         => "Asiática",
            TradingSession.London        => "Londres",
            TradingSession.NewYork       => "Nueva York",
            TradingSession.Sydney        => "Sydney",
            TradingSession.LondonNewYork => "Londres / NY",
            _                            => "—"
        };

        TimeframeText.Text = string.IsNullOrWhiteSpace(_trade.Timeframe) ? "—" : _trade.Timeframe;

        TradingTypeText.Text = _trade.TradingType switch
        {
            TradingType.Scalping => "Scalping",
            TradingType.Intraday => "Intradía",
            TradingType.Swing    => "Swing Trading",
            TradingType.Position => "Trading de posición",
            _                    => "—"
        };

        EmotionalStateText.Text = string.IsNullOrWhiteSpace(_trade.EmotionalState) ? "—" : _trade.EmotionalState;

        MistakeTypeText.Text = string.IsNullOrWhiteSpace(_trade.MistakeType) ? "—" : _trade.MistakeType;
        RatingText.Text      = _trade.Rating.HasValue ? $"{_trade.Rating.Value} / 10" : "—";

        if (!string.IsNullOrWhiteSpace(_trade.Notes))
        {
            NotesText.Text       = _trade.Notes;
            NotesPanel.Visibility = Visibility.Visible;
        }

        if (!string.IsNullOrWhiteSpace(_trade.ScreenshotUrl))
        {
            UrlText.Text          = _trade.ScreenshotUrl;
            UrlPanel.Visibility   = Visibility.Visible;
            OpenUrlBtn.Visibility  = Visibility.Visible;
            NoImageBorder.Visibility = Visibility.Collapsed;
        }
    }

    private void OnOpenUrl(object sender, RoutedEventArgs e) => OpenUrl();
    private void OnOpenUrl(object sender, MouseButtonEventArgs e) => OpenUrl();

    private void OpenUrl()
    {
        var url = _trade.ScreenshotUrl;
        if (string.IsNullOrWhiteSpace(url)) return;
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch { /* ignorar */ }
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }
}
