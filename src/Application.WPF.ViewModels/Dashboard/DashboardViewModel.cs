using Application.WPF.Common.ViewModels;
using Application.WPF.Models.Entities;
using Application.WPF.Models.Enums;
using Application.WPF.ViewModels.Journal;
using Application.WPF.ViewModels.Playbook;
using Application.WPF.ViewModels.Strategies;
using Application.WPF.ViewModels.TradingAccount;
using TradingAccountEntity = Application.WPF.Models.Entities.TradingAccount;
using Application.WPF.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace Application.WPF.ViewModels;

// ── Helper display models ──────────────────────────────────────────────────

public class StrategyStatItem
{
    public string Name        { get; set; } = string.Empty;
    public int    TradeCount  { get; set; }
    public double WinRate     { get; set; }
    public decimal PnL        { get; set; }
    public double  BarWidth   { get; set; }  // 0–1, normalised to highest WinRate
    public string  WinRateLabel => $"{WinRate:F1}%";
    public string  PnLLabel     => PnL >= 0 ? $"+{PnL:N2}" : $"{PnL:N2}";
    public bool    IsPnLPositive => PnL >= 0;
}

public class SymbolStatItem
{
    public string  Symbol   { get; set; } = string.Empty;
    public int     Count    { get; set; }
    public decimal PnL      { get; set; }
    public double  BarPct   { get; set; }  // 0–1
    public bool    IsPnLPositive => PnL >= 0;
    public string  PnLLabel      => PnL >= 0 ? $"+{PnL:N2}" : $"{PnL:N2}";
}

public class SessionStatItem
{
    public string  Label    { get; set; } = string.Empty;
    public int     Count    { get; set; }
    public decimal PnL      { get; set; }
    public double  BarPct   { get; set; }
    public bool    IsPnLPositive => PnL >= 0;
}

public class RecentTradeItem
{
    public string   Symbol    { get; set; } = string.Empty;
    public string   Direction { get; set; } = string.Empty;
    public string   Result    { get; set; } = string.Empty;
    public decimal  PnL       { get; set; }
    public string   Date      { get; set; } = string.Empty;
    public bool     IsProfit  { get; set; }
    public bool     IsLoss    { get; set; }
    public string   PnLLabel  => PnL >= 0 ? $"+{PnL:N2}" : $"{PnL:N2}";
}

// ── ViewModel ─────────────────────────────────────────────────────────────

public partial class DashboardViewModel : BaseViewModel
{
    private readonly ITradeService           _tradeService;
    private readonly ITradingAccountService  _accountService;
    private readonly ITradingStrategyService _strategyService;
    private readonly IPlaybookService        _playbookService;
    private readonly ISessionService         _sessionService;
    private readonly INavigationService      _navigationService;
    private readonly ILogger<DashboardViewModel> _logger;

    // ── KPIs ──────────────────────────────────────────────────────────────
    [ObservableProperty] private int     _totalTrades;
    [ObservableProperty] private int     _winCount;
    [ObservableProperty] private int     _lossCount;
    [ObservableProperty] private int     _openCount;
    [ObservableProperty] private string  _winRateLabel    = "—";
    [ObservableProperty] private string  _avgRRLabel      = "—";
    [ObservableProperty] private string  _profitFactorLabel = "—";
    [ObservableProperty] private string  _netPnlMonthLabel = "$0.00";
    [ObservableProperty] private string  _netPnlTotalLabel = "$0.00";
    [ObservableProperty] private bool    _netPnlMonthPositive = true;
    [ObservableProperty] private bool    _netPnlTotalPositive = true;
    [ObservableProperty] private string  _bestStreakLabel  = "0";

    // ── Donut / win-loss ──────────────────────────────────────────────────
    [ObservableProperty] private double  _winPct;
    [ObservableProperty] private double  _lossPct;
    [ObservableProperty] private double  _openPct;

    // ── Capital summary ───────────────────────────────────────────────────
    [ObservableProperty] private string  _totalInitialCapital = "$0.00";
    [ObservableProperty] private string  _totalCurrentCapital = "$0.00";
    [ObservableProperty] private string  _totalGainLoss       = "$0.00";
    [ObservableProperty] private string  _totalGainLossPct    = "0.00%";
    [ObservableProperty] private bool    _isCapitalPositive   = true;
    [ObservableProperty] private int     _accountCount;

    // ── Equity curve data (passed to code-behind for drawing) ─────────────
    private List<double>   _equityValues = new();
    private List<DateTime> _equityDates  = new();
    public  IReadOnlyList<double>   EquityValues => _equityValues;
    public  IReadOnlyList<DateTime> EquityDates  => _equityDates;
    public event EventHandler? EquityUpdated;

    // ── Lists ─────────────────────────────────────────────────────────────
    public ObservableCollection<RecentTradeItem>  RecentTrades   { get; } = new();
    public ObservableCollection<StrategyStatItem> StrategyStats  { get; } = new();
    public ObservableCollection<SymbolStatItem>   SymbolStats    { get; } = new();
    public ObservableCollection<SessionStatItem>  SessionStats   { get; } = new();

    // ── Playbook summary ──────────────────────────────────────────────────
    [ObservableProperty] private int    _playbookCount;
    [ObservableProperty] private string _playbookAvgRatingLabel = "—";
    [ObservableProperty] private bool   _hasPlaybook;

    // ── Estado ────────────────────────────────────────────────────────────
    [ObservableProperty] private string _lastRefreshed = string.Empty;
    [ObservableProperty] private bool   _hasData;

    public DashboardViewModel(
        ITradeService           tradeService,
        ITradingAccountService  accountService,
        ITradingStrategyService strategyService,
        IPlaybookService        playbookService,
        ISessionService         sessionService,
        INavigationService      navigationService,
        ILogger<DashboardViewModel> logger)
    {
        _tradeService      = tradeService;
        _accountService    = accountService;
        _strategyService   = strategyService;
        _playbookService   = playbookService;
        _sessionService    = sessionService;
        _navigationService = navigationService;
        _logger            = logger;
        Title              = "Panel";
    }

    // ── Navigation commands ───────────────────────────────────────────────

    [RelayCommand]
    private void GoToJournal()    => _navigationService.NavigateTo<TradeJournalViewModel>();

    [RelayCommand]
    private void GoToAnalytics()  => _navigationService.NavigateTo<TradeAnalyticsViewModel>();

    [RelayCommand]
    private void GoToStrategies() => _navigationService.NavigateTo<TradingStrategyViewModel>();

    [RelayCommand]
    private void GoToPlaybook()   => _navigationService.NavigateTo<PlaybookViewModel>();

    [RelayCommand]
    private void GoToAccount()    => _navigationService.NavigateTo<TradingAccountViewModel>();

    public override async Task InitializeAsync()
    {
        await LoadDataAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadDataAsync();

    // ── Core data load ────────────────────────────────────────────────────

    private async Task LoadDataAsync()
    {
        if (_sessionService.CurrentUser is not { } user) return;

        IsBusy = true;
        try
        {
            var (trades, accounts, playbook) = await FetchAllDataAsync(user.Id);

            ComputeKpis(trades);
            ComputeCapitalSummary(accounts, trades);
            BuildRecentTrades(trades);
            BuildStrategyStats(trades);
            BuildSymbolStats(trades);
            BuildSessionStats(trades);
            BuildEquityCurve(trades, accounts);
            BuildPlaybookSummary(playbook);

            HasData         = trades.Count > 0;
            LastRefreshed   = $"Actualizado {DateTime.Now:HH:mm}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading dashboard data");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<(IReadOnlyList<TradeEntry> trades, IReadOnlyList<TradingAccountEntity> accounts, IReadOnlyList<PlaybookEntry> playbook)>
        FetchAllDataAsync(int userId)
    {
        var tradesTask   = _tradeService.GetAllByUserIdAsync(userId);
        var accountsTask = _accountService.GetAllByUserIdAsync(userId);
        var playbookTask = _playbookService.GetAllByUserIdAsync(userId);

        await Task.WhenAll(tradesTask, accountsTask, playbookTask);

        return (tradesTask.Result, accountsTask.Result, playbookTask.Result);
    }

    // ── KPI computation ───────────────────────────────────────────────────

    private void ComputeKpis(IReadOnlyList<TradeEntry> trades)
    {
        var closed = trades.Where(t => t.Result is TradeResult.Profit or TradeResult.Loss or TradeResult.BreakEven).ToList();
        var wins   = closed.Where(t => t.Result == TradeResult.Profit).ToList();
        var losses = closed.Where(t => t.Result == TradeResult.Loss).ToList();
        var opens  = trades.Where(t => t.Result == TradeResult.Open).ToList();

        TotalTrades = trades.Count;
        WinCount    = wins.Count;
        LossCount   = losses.Count;
        OpenCount   = opens.Count;

        // Win rate
        WinRateLabel = closed.Count > 0
            ? $"{(double)wins.Count / closed.Count * 100:F1}%"
            : "—";

        // Win/Loss pcts for bar
        if (TotalTrades > 0)
        {
            WinPct  = (double)WinCount  / TotalTrades * 100;
            LossPct = (double)LossCount / TotalTrades * 100;
            OpenPct = (double)OpenCount / TotalTrades * 100;
        }

        // Avg R:R
        var rrValues = closed.Where(t => t.RiskRewardRatio.HasValue).Select(t => (double)t.RiskRewardRatio!.Value).ToList();
        AvgRRLabel = rrValues.Count > 0 ? $"{rrValues.Average():F1}:1" : "—";

        // Profit factor = gross profit / |gross loss|
        var grossProfit = wins.Where(t => t.ProfitLoss.HasValue).Sum(t => t.ProfitLoss!.Value);
        var grossLoss   = Math.Abs(losses.Where(t => t.ProfitLoss.HasValue).Sum(t => t.ProfitLoss!.Value));
        ProfitFactorLabel = grossLoss > 0 ? $"{grossProfit / grossLoss:F2}" : wins.Count > 0 ? "∞" : "—";

        // Net P&L this month
        var thisMonth  = DateTime.Now;
        var monthTrades = trades.Where(t =>
            t.ExitDate.HasValue &&
            t.ExitDate.Value.Year  == thisMonth.Year &&
            t.ExitDate.Value.Month == thisMonth.Month &&
            t.ProfitLoss.HasValue).ToList();

        var netMonth = monthTrades.Sum(t => t.ProfitLoss!.Value);
        NetPnlMonthLabel    = netMonth >= 0 ? $"+{netMonth:N2}" : $"{netMonth:N2}";
        NetPnlMonthPositive = netMonth >= 0;

        // Net P&L total
        var netTotal = trades.Where(t => t.ProfitLoss.HasValue).Sum(t => t.ProfitLoss!.Value);
        NetPnlTotalLabel    = netTotal >= 0 ? $"+{netTotal:N2}" : $"{netTotal:N2}";
        NetPnlTotalPositive = netTotal >= 0;

        // Best win streak
        BestStreakLabel = ComputeBestWinStreak(closed).ToString();
    }

    private static int ComputeBestWinStreak(IEnumerable<TradeEntry> sortedClosed)
    {
        int best = 0, current = 0;
        foreach (var t in sortedClosed.OrderBy(t => t.ExitDate ?? t.EntryDate))
        {
            if (t.Result == TradeResult.Profit) { current++; best = Math.Max(best, current); }
            else current = 0;
        }
        return best;
    }

    // ── Capital summary ───────────────────────────────────────────────────

    private void ComputeCapitalSummary(IReadOnlyList<TradingAccountEntity> accounts, IReadOnlyList<TradeEntry> trades)
    {
        AccountCount = accounts.Count;

        var initial = accounts.Sum(a => a.InitialCapital);
        var pnl     = trades.Where(t => t.ProfitLoss.HasValue).Sum(t => t.ProfitLoss!.Value);
        var current = initial + pnl;
        var pct     = initial > 0 ? pnl / initial * 100 : 0;

        TotalInitialCapital = $"${initial:N2}";
        TotalCurrentCapital = $"${current:N2}";
        IsCapitalPositive   = pnl >= 0;
        TotalGainLoss       = pnl >= 0 ? $"+${pnl:N2}" : $"-${Math.Abs(pnl):N2}";
        TotalGainLossPct    = pct  >= 0 ? $"+{pct:F2}%" : $"{pct:F2}%";
    }

    // ── Recent trades ─────────────────────────────────────────────────────

    private void BuildRecentTrades(IReadOnlyList<TradeEntry> trades)
    {
        RecentTrades.Clear();
        var recent = trades
            .OrderByDescending(t => t.ExitDate ?? t.EntryDate)
            .Take(8)
            .ToList();

        foreach (var t in recent)
        {
            RecentTrades.Add(new RecentTradeItem
            {
                Symbol    = t.Symbol,
                Direction = t.Direction == TradeDirection.Long ? "L" : "S",
                Result    = t.Result.ToString(),
                PnL       = t.ProfitLoss ?? 0m,
                Date      = (t.ExitDate ?? t.EntryDate).ToString("dd/MM"),
                IsProfit  = t.Result == TradeResult.Profit,
                IsLoss    = t.Result == TradeResult.Loss,
            });
        }
    }

    // ── Strategy stats ────────────────────────────────────────────────────

    private void BuildStrategyStats(IReadOnlyList<TradeEntry> trades)
    {
        StrategyStats.Clear();

        var groups = trades
            .GroupBy(t => t.Strategy?.Title ?? "Sin estrategia")
            .Select(g =>
            {
                var closed = g.Where(t => t.Result is TradeResult.Profit or TradeResult.Loss).ToList();
                var wins   = closed.Count(t => t.Result == TradeResult.Profit);
                var pnl    = g.Where(t => t.ProfitLoss.HasValue).Sum(t => t.ProfitLoss!.Value);
                return new StrategyStatItem
                {
                    Name       = g.Key,
                    TradeCount = g.Count(),
                    WinRate    = closed.Count > 0 ? (double)wins / closed.Count * 100 : 0,
                    PnL        = pnl,
                };
            })
            .OrderByDescending(s => s.TradeCount)
            .Take(5)
            .ToList();

        var maxWR = groups.Count > 0 ? groups.Max(s => s.WinRate) : 1;
        foreach (var s in groups)
        {
            s.BarWidth = maxWR > 0 ? s.WinRate / maxWR : 0;
            StrategyStats.Add(s);
        }
    }

    // ── Symbol stats ──────────────────────────────────────────────────────

    private void BuildSymbolStats(IReadOnlyList<TradeEntry> trades)
    {
        SymbolStats.Clear();

        var groups = trades
            .GroupBy(t => t.Symbol)
            .Select(g => new SymbolStatItem
            {
                Symbol = g.Key,
                Count  = g.Count(),
                PnL    = g.Where(t => t.ProfitLoss.HasValue).Sum(t => t.ProfitLoss!.Value),
            })
            .OrderByDescending(s => s.Count)
            .Take(5)
            .ToList();

        var max = groups.Count > 0 ? groups.Max(s => s.Count) : 1;
        foreach (var s in groups)
        {
            s.BarPct = max > 0 ? (double)s.Count / max : 0;
            SymbolStats.Add(s);
        }
    }

    // ── Session stats ─────────────────────────────────────────────────────

    private void BuildSessionStats(IReadOnlyList<TradeEntry> trades)
    {
        SessionStats.Clear();

        var groups = trades
            .Where(t => t.Session.HasValue)
            .GroupBy(t => t.Session!.Value)
            .Select(g => new SessionStatItem
            {
                Label = g.Key switch
                {
                    TradingSession.Asian          => "Asian",
                    TradingSession.London         => "London",
                    TradingSession.NewYork        => "New York",
                    TradingSession.Sydney         => "Sydney",
                    TradingSession.LondonNewYork  => "Lon/NY",
                    _                             => g.Key.ToString()
                },
                Count = g.Count(),
                PnL   = g.Where(t => t.ProfitLoss.HasValue).Sum(t => t.ProfitLoss!.Value),
            })
            .OrderByDescending(s => s.Count)
            .ToList();

        var max = groups.Count > 0 ? groups.Max(s => s.Count) : 1;
        foreach (var s in groups)
        {
            s.BarPct = max > 0 ? (double)s.Count / max : 0;
            SessionStats.Add(s);
        }
    }

    // ── Equity curve ──────────────────────────────────────────────────────

    private void BuildEquityCurve(IReadOnlyList<TradeEntry> trades, IReadOnlyList<TradingAccountEntity> accounts)
    {
        var initial      = (double)(accounts.Sum(a => a.InitialCapital));
        var startDate    = accounts.Count > 0 ? accounts.Min(a => a.StartDate) : DateTime.Today;
        var closedTrades = trades
            .Where(t => t.ProfitLoss.HasValue && (t.ExitDate ?? t.EntryDate) != default)
            .OrderBy(t => t.ExitDate ?? t.EntryDate)
            .ToList();

        _equityValues = new List<double>   { initial };
        _equityDates  = new List<DateTime> { startDate };
        double cum    = initial;
        foreach (var t in closedTrades)
        {
            cum += (double)t.ProfitLoss!.Value;
            _equityValues.Add(cum);
            _equityDates.Add(t.ExitDate ?? t.EntryDate);
        }

        EquityUpdated?.Invoke(this, EventArgs.Empty);
    }

    // ── Playbook summary ──────────────────────────────────────────────────

    private void BuildPlaybookSummary(IReadOnlyList<PlaybookEntry> entries)
    {
        PlaybookCount = entries.Count;
        HasPlaybook   = entries.Count > 0;

        var rated = entries.Where(e => e.ManualRating.HasValue).ToList();
        PlaybookAvgRatingLabel = rated.Count > 0
            ? $"★ {rated.Average(e => e.ManualRating!.Value):F1}"
            : entries.Count > 0 ? "Sin calificar" : "—";
    }
}
