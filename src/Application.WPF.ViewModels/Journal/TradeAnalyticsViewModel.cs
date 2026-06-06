using Application.WPF.Common.ViewModels;
using Application.WPF.Models.Entities;
using Application.WPF.Models.Enums;
using Application.WPF.Services.Interfaces;
using ClosedXML.Excel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Globalization;
using TradingAccountEntity = Application.WPF.Models.Entities.TradingAccount;

namespace Application.WPF.ViewModels.Journal;

public partial class TradeAnalyticsViewModel : BaseViewModel
{
    private readonly ITradeService            _tradeService;
    private readonly ITradingAccountService   _accountService;
    private readonly ITradingStrategyService  _strategyService;
    private readonly ISessionService          _sessionService;
    private readonly ILogger<TradeAnalyticsViewModel> _logger;

    // ── Catálogos para filtros ────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilterAccounts))]
    private ObservableCollection<TradingAccountEntity> _accounts = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilterStrategies))]
    private ObservableCollection<TradingStrategy> _strategies = new();

    public IReadOnlyList<TradingAccountEntity?> FilterAccounts =>
        new[] { (TradingAccountEntity?)null }.Concat(Accounts).ToList();

    public IReadOnlyList<TradingStrategy?> FilterStrategies =>
        new[] { (TradingStrategy?)null }.Concat(Strategies).ToList();

    public IReadOnlyList<int?> RatingOptions { get; } =
        new List<int?> { null, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

    // ── Filtros ───────────────────────────────────────────────────────────

    [ObservableProperty] private TradingAccountEntity? _filterAccount;
    [ObservableProperty] private TradingStrategy?      _filterStrategy;
    [ObservableProperty] private int?                  _filterRatingMin;
    [ObservableProperty] private int?                  _filterRatingMax;
    [ObservableProperty] private DateTime?             _filterDateFrom = DateTime.Today.AddMonths(-3);
    [ObservableProperty] private DateTime?             _filterDateTo   = DateTime.Today;

    // ── Estado ────────────────────────────────────────────────────────────

    [ObservableProperty] private bool   _hasData;
    [ObservableProperty] private string _generalError = string.Empty;

    // ── KPIs ─────────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WinBarWidth), nameof(LossBarWidth), nameof(BEBarWidth))]
    private int _totalTrades;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WinBarWidth))]
    private int _winningTrades;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LossBarWidth))]
    private int _losingTrades;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BEBarWidth))]
    private int _breakEvenTrades;

    [ObservableProperty] private int    _openTrades;
    [ObservableProperty] private double _winRatePct;
    [ObservableProperty] private string _winRateDisplay      = "—";
    [ObservableProperty] private string _totalPLDisplay      = "—";
    [ObservableProperty] private bool   _totalPLPositive;
    [ObservableProperty] private string _grossProfitDisplay  = "—";
    [ObservableProperty] private string _grossLossDisplay    = "—";
    [ObservableProperty] private string _profitFactorDisplay = "—";
    [ObservableProperty] private string _avgPLDisplay        = "—";
    [ObservableProperty] private bool   _avgPLPositive;
    [ObservableProperty] private string _avgRRDisplay        = "—";
    [ObservableProperty] private string _bestTradeDisplay    = "—";
    [ObservableProperty] private string _worstTradeDisplay   = "—";
    [ObservableProperty] private int    _maxWinStreak;
    [ObservableProperty] private int    _maxLossStreak;
    [ObservableProperty] private string _avgRatingDisplay    = "—";
    [ObservableProperty] private string _expectancyDisplay   = "—";
    [ObservableProperty] private bool   _expectancyPositive;

    // Ancho de barras de distribución de resultados (max 400px)
    public double WinBarWidth  => TotalTrades > 0 ? (double)WinningTrades   / TotalTrades * 400 : 0;
    public double LossBarWidth => TotalTrades > 0 ? (double)LosingTrades    / TotalTrades * 400 : 0;
    public double BEBarWidth   => TotalTrades > 0 ? (double)BreakEvenTrades / TotalTrades * 400 : 0;

    public double WinPct  => TotalTrades > 0 ? (double)WinningTrades   / TotalTrades * 100 : 0;
    public double LossPct => TotalTrades > 0 ? (double)LosingTrades    / TotalTrades * 100 : 0;
    public double BEPct   => TotalTrades > 0 ? (double)BreakEvenTrades / TotalTrades * 100 : 0;

    // ── Desgloses ────────────────────────────────────────────────────────

    // Colecciones completas (para cómputo interno y export)
    private List<SymbolAnalytics>   _allBySymbol   = new();
    private List<StrategyAnalytics> _allByStrategy = new();

    // Colecciones paginadas (bound en la View)
    [ObservableProperty] private ObservableCollection<SymbolAnalytics>    _bySymbol    = new();
    [ObservableProperty] private ObservableCollection<StrategyAnalytics>  _byStrategy  = new();
    [ObservableProperty] private ObservableCollection<SessionAnalytics>   _bySession   = new();
    [ObservableProperty] private ObservableCollection<DirectionAnalytics> _byDirection = new();
    [ObservableProperty] private ObservableCollection<MonthlyAnalytics>   _byMonth     = new();

    // ── Paginación de Símbolo ─────────────────────────────────────────────

    private const int TablePageSize = 8;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SymbolPageInfo), nameof(SymbolIsFirstPage), nameof(SymbolIsLastPage))]
    private int _symbolCurrentPage = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SymbolPageInfo), nameof(SymbolIsLastPage))]
    private int _symbolTotalPages = 1;

    public string SymbolPageInfo   => $"{SymbolCurrentPage} / {SymbolTotalPages}";
    public bool   SymbolIsFirstPage => SymbolCurrentPage <= 1;
    public bool   SymbolIsLastPage  => SymbolCurrentPage >= SymbolTotalPages;

    // ── Paginación de Estrategia ──────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StrategyPageInfo), nameof(StrategyIsFirstPage), nameof(StrategyIsLastPage))]
    private int _strategyCurrentPage = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StrategyPageInfo), nameof(StrategyIsLastPage))]
    private int _strategyTotalPages = 1;

    public string StrategyPageInfo    => $"{StrategyCurrentPage} / {StrategyTotalPages}";
    public bool   StrategyIsFirstPage => StrategyCurrentPage <= 1;
    public bool   StrategyIsLastPage  => StrategyCurrentPage >= StrategyTotalPages;

    // ── Datos de gráficos ─────────────────────────────────────────────────

    [ObservableProperty] private ObservableCollection<MonthlyChartPoint>  _monthlyChart  = new();
    [ObservableProperty] private ObservableCollection<SessionChartPoint>  _sessionChart  = new();

    // ── Constructor ───────────────────────────────────────────────────────

    public TradeAnalyticsViewModel(
        ITradeService tradeService,
        ITradingAccountService accountService,
        ITradingStrategyService strategyService,
        ISessionService sessionService,
        ILogger<TradeAnalyticsViewModel> logger)
    {
        _tradeService    = tradeService;
        _accountService  = accountService;
        _strategyService = strategyService;
        _sessionService  = sessionService;
        _logger          = logger;
        Title            = "Análisis de Trading";
    }

    public override async Task InitializeAsync()
    {
        var user = _sessionService.CurrentUser;
        if (user is null) return;

        var accs   = await _accountService.GetAllByUserIdAsync(user.Id);
        Accounts   = new ObservableCollection<TradingAccountEntity>(accs);
        var strats = await _strategyService.GetAllByUserIdAsync(user.Id);
        Strategies = new ObservableCollection<TradingStrategy>(strats);

        await ComputeAsync();
    }

    // ── Comandos ─────────────────────────────────────────────────────────

    [RelayCommand] private void NextSymbolPage()     { if (!SymbolIsLastPage)    { SymbolCurrentPage++;    RefreshSymbolPage(); } }
    [RelayCommand] private void PrevSymbolPage()     { if (!SymbolIsFirstPage)   { SymbolCurrentPage--;    RefreshSymbolPage(); } }
    [RelayCommand] private void NextStrategyPage()   { if (!StrategyIsLastPage)  { StrategyCurrentPage++;  RefreshStrategyPage(); } }
    [RelayCommand] private void PrevStrategyPage()   { if (!StrategyIsFirstPage) { StrategyCurrentPage--;  RefreshStrategyPage(); } }

    private void RefreshSymbolPage()
    {
        SymbolTotalPages  = Math.Max(1, (int)Math.Ceiling((double)_allBySymbol.Count   / TablePageSize));
        if (SymbolCurrentPage > SymbolTotalPages) SymbolCurrentPage = SymbolTotalPages;
        BySymbol   = new ObservableCollection<SymbolAnalytics>(
            _allBySymbol.Skip((SymbolCurrentPage - 1) * TablePageSize).Take(TablePageSize));
    }

    private void RefreshStrategyPage()
    {
        StrategyTotalPages = Math.Max(1, (int)Math.Ceiling((double)_allByStrategy.Count / TablePageSize));
        if (StrategyCurrentPage > StrategyTotalPages) StrategyCurrentPage = StrategyTotalPages;
        ByStrategy = new ObservableCollection<StrategyAnalytics>(
            _allByStrategy.Skip((StrategyCurrentPage - 1) * TablePageSize).Take(TablePageSize));
    }

    [RelayCommand]
    private async Task ApplyFilterAsync() => await ComputeAsync();

    [RelayCommand]
    private async Task ClearFilterAsync()
    {
        FilterAccount   = null;
        FilterStrategy  = null;
        FilterRatingMin = null;
        FilterRatingMax = null;
        FilterDateFrom  = DateTime.Today.AddMonths(-3);
        FilterDateTo    = DateTime.Today;
        await ComputeAsync();
    }

    [RelayCommand]
    private void ExportToExcel()
    {
        if (!HasData) { GeneralError = "No hay datos para exportar."; return; }

        var dlg = new SaveFileDialog
        {
            Title    = "Exportar Análisis de Trading",
            Filter   = "Excel (*.xlsx)|*.xlsx",
            FileName = $"analisis_{DateTime.Today:yyyyMMdd}.xlsx"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            using var wb = new XLWorkbook();
            BuildSummarySheet(wb);
            BuildMonthlySheet(wb);
            BuildSymbolSheet(wb);
            BuildStrategySheet(wb);
            BuildSessionSheet(wb);
            wb.SaveAs(dlg.FileName);
            GeneralError = string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting analytics");
            GeneralError = "Error al exportar el análisis.";
        }
    }

    // ── Cálculo principal ─────────────────────────────────────────────────

    private async Task ComputeAsync()
    {
        GeneralError = string.Empty;
        var user = _sessionService.CurrentUser;
        if (user is null) return;

        IsBusy = true;
        try
        {
            var all      = await _tradeService.GetAllByUserIdAsync(user.Id);
            var filtered = ApplyFilters(all).ToList();

            HasData = filtered.Any();
            if (!HasData) { ResetKpis(); return; }

            ComputeKpis(filtered);
            ComputeBySymbol(filtered);
            ComputeByStrategy(filtered);
            ComputeBySession(filtered);
            ComputeByDirection(filtered);
            ComputeByMonth(filtered);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error computing analytics");
            GeneralError = "Error al calcular el análisis.";
        }
        finally { IsBusy = false; }
    }

    private IEnumerable<TradeEntry> ApplyFilters(IEnumerable<TradeEntry> trades)
    {
        if (FilterAccount  is not null) trades = trades.Where(t => t.AccountId  == FilterAccount.Id);
        if (FilterStrategy is not null) trades = trades.Where(t => t.StrategyId == FilterStrategy.Id);
        if (FilterRatingMin.HasValue)   trades = trades.Where(t => t.Rating.HasValue && t.Rating >= FilterRatingMin);
        if (FilterRatingMax.HasValue)   trades = trades.Where(t => t.Rating.HasValue && t.Rating <= FilterRatingMax);
        if (FilterDateFrom.HasValue)    trades = trades.Where(t => t.EntryDate.Date >= FilterDateFrom.Value.Date);
        if (FilterDateTo.HasValue)      trades = trades.Where(t => t.EntryDate.Date <= FilterDateTo.Value.Date);
        return trades;
    }

    // ── KPIs ─────────────────────────────────────────────────────────────

    private void ComputeKpis(List<TradeEntry> trades)
    {
        TotalTrades     = trades.Count;
        WinningTrades   = trades.Count(t => t.Result == TradeResult.Profit);
        LosingTrades    = trades.Count(t => t.Result == TradeResult.Loss);
        BreakEvenTrades = trades.Count(t => t.Result == TradeResult.BreakEven);
        OpenTrades      = trades.Count(t => t.Result == TradeResult.Open);

        int closed = WinningTrades + LosingTrades + BreakEvenTrades;
        double wr  = closed > 0 ? (double)WinningTrades / closed * 100.0 : 0;
        WinRatePct     = wr;
        WinRateDisplay = closed > 0 ? $"{wr:F1}%" : "—";

        var pl      = trades.Where(t => t.ProfitLoss.HasValue).ToList();
        decimal gP  = pl.Where(t => t.ProfitLoss > 0).Sum(t => t.ProfitLoss!.Value);
        decimal gL  = Math.Abs(pl.Where(t => t.ProfitLoss < 0).Sum(t => t.ProfitLoss!.Value));
        decimal tot = gP - gL;

        TotalPLDisplay     = pl.Any() ? $"{tot:+#,##0.00;-#,##0.00;0}" : "—";
        TotalPLPositive    = tot >= 0;
        GrossProfitDisplay = pl.Any() ? $"+{gP:N2}" : "—";
        GrossLossDisplay   = pl.Any() ? $"-{gL:N2}"  : "—";

        double pf = gL > 0 ? (double)(gP / gL) : (gP > 0 ? 99.99 : 0);
        ProfitFactorDisplay = pl.Any() ? $"{pf:F2}" : "—";

        decimal avg = pl.Any() ? tot / pl.Count : 0;
        AvgPLDisplay  = pl.Any() ? $"{avg:+#,##0.00;-#,##0.00;0}" : "—";
        AvgPLPositive = avg >= 0;

        var rr = trades.Where(t => t.RiskRewardRatio.HasValue).ToList();
        AvgRRDisplay = rr.Any() ? $"{rr.Average(t => (double)t.RiskRewardRatio!.Value):F2}" : "—";

        if (pl.Any())
        {
            BestTradeDisplay  = $"+{pl.Max(t => t.ProfitLoss!.Value):N2}";
            WorstTradeDisplay = $"{pl.Min(t => t.ProfitLoss!.Value):N2}";
        }

        var sorted = trades.OrderBy(t => t.EntryDate).Select(t => t.Result).ToList();
        (MaxWinStreak, MaxLossStreak) = ComputeStreaks(sorted);

        var rated = trades.Where(t => t.Rating.HasValue).ToList();
        AvgRatingDisplay = rated.Any() ? $"{rated.Average(t => (double)t.Rating!.Value):F1}" : "—";

        if (WinningTrades > 0 && LosingTrades > 0 && pl.Any())
        {
            decimal avgW = pl.Where(t => t.ProfitLoss > 0).Average(t => t.ProfitLoss!.Value);
            decimal avgLv= pl.Where(t => t.ProfitLoss < 0).Average(t => t.ProfitLoss!.Value);
            double  wrD  = WinRatePct / 100.0;
            decimal exp  = (decimal)wrD * avgW + (decimal)(1 - wrD) * avgLv;
            ExpectancyDisplay  = $"{exp:+#,##0.00;-#,##0.00;0}";
            ExpectancyPositive = exp >= 0;
        }
        else { ExpectancyDisplay = "—"; ExpectancyPositive = false; }

        // Notify computed bar widths
        OnPropertyChanged(nameof(WinPct));
        OnPropertyChanged(nameof(LossPct));
        OnPropertyChanged(nameof(BEPct));
    }

    private static (int win, int loss) ComputeStreaks(List<TradeResult> r)
    {
        int mW = 0, mL = 0, cW = 0, cL = 0;
        foreach (var x in r)
        {
            if      (x == TradeResult.Profit) { cW++; cL = 0; mW = Math.Max(mW, cW); }
            else if (x == TradeResult.Loss)   { cL++; cW = 0; mL = Math.Max(mL, cL); }
            else                              { cW = 0; cL = 0; }
        }
        return (mW, mL);
    }

    // ── Desgloses ────────────────────────────────────────────────────────

    private void ComputeBySymbol(List<TradeEntry> t)
    {
        _allBySymbol = t.GroupBy(x => x.Symbol)
                        .Select(g => MakeSymbol(g.Key, g.ToList()))
                        .OrderByDescending(s => s.TotalPL)
                        .ToList();
        SymbolCurrentPage = 1;
        RefreshSymbolPage();
    }

    private void ComputeByStrategy(List<TradeEntry> t)
    {
        _allByStrategy = t.GroupBy(x => x.Strategy?.Title ?? "Sin estrategia")
                          .Select(g => MakeStrategy(g.Key, g.ToList()))
                          .OrderByDescending(s => s.TotalPL)
                          .ToList();
        StrategyCurrentPage = 1;
        RefreshStrategyPage();
    }

    private void ComputeBySession(List<TradeEntry> t)
    {
        var sessions = t.Where(x => x.Session.HasValue)
                        .GroupBy(x => x.Session!.Value)
                        .Select(g => MakeSession(g.Key, g.ToList()))
                        .OrderByDescending(s => s.Total)
                        .ToList();
        BySession = new ObservableCollection<SessionAnalytics>(sessions);

        // Chart: barras de win rate por sesión (max 240px)
        SessionChart = new ObservableCollection<SessionChartPoint>(
            sessions.Select(s => new SessionChartPoint(s.Session, s.WinRate, s.WinRate / 100.0 * 240)));
    }

    private void ComputeByDirection(List<TradeEntry> t)
        => ByDirection = new ObservableCollection<DirectionAnalytics>(
            t.GroupBy(x => x.Direction)
             .Select(g => MakeDirection(g.Key, g.ToList()))
             .OrderBy(d => d.SortOrder));

    private void ComputeByMonth(List<TradeEntry> t)
    {
        var groups = t.GroupBy(x => new { x.EntryDate.Year, x.EntryDate.Month })
                      .Select(g => MakeMonth(g.Key.Year, g.Key.Month, g.ToList()))
                      .OrderByDescending(m => m.SortKey)
                      .ToList();
        ByMonth = new ObservableCollection<MonthlyAnalytics>(groups);

        // Chart: barras horizontales de P&L mensual (max 300px)
        double maxAbs = groups.Any() ? (double)groups.Max(m => Math.Abs(m.TotalPL)) : 1;
        if (maxAbs == 0) maxAbs = 1;
        MonthlyChart = new ObservableCollection<MonthlyChartPoint>(
            groups.Select(m => new MonthlyChartPoint(
                m.Month,
                m.TotalPL,
                m.TotalPL >= 0,
                (double)Math.Abs(m.TotalPL) / maxAbs * 300)));
    }

    // ── Builders ─────────────────────────────────────────────────────────

    private static SymbolAnalytics MakeSymbol(string sym, List<TradeEntry> t)
    {
        int w = t.Count(x => x.Result == TradeResult.Profit);
        int l = t.Count(x => x.Result == TradeResult.Loss);
        int c = w + l + t.Count(x => x.Result == TradeResult.BreakEven);
        decimal pl = t.Where(x => x.ProfitLoss.HasValue).Sum(x => x.ProfitLoss!.Value);
        double  wr = c > 0 ? Math.Round((double)w / c * 100, 1) : 0;
        double  rr = t.Where(x => x.RiskRewardRatio.HasValue)
                      .Select(x => (double)x.RiskRewardRatio!.Value).DefaultIfEmpty(0).Average();
        return new SymbolAnalytics(sym, t.Count, w, l, wr, pl, Math.Round(rr, 2));
    }

    private static StrategyAnalytics MakeStrategy(string s, List<TradeEntry> t)
    {
        int w = t.Count(x => x.Result == TradeResult.Profit);
        int l = t.Count(x => x.Result == TradeResult.Loss);
        int c = w + l + t.Count(x => x.Result == TradeResult.BreakEven);
        decimal pl = t.Where(x => x.ProfitLoss.HasValue).Sum(x => x.ProfitLoss!.Value);
        double  wr = c > 0 ? Math.Round((double)w / c * 100, 1) : 0;
        double  rr = t.Where(x => x.RiskRewardRatio.HasValue)
                      .Select(x => (double)x.RiskRewardRatio!.Value).DefaultIfEmpty(0).Average();
        double  ar = t.Where(x => x.Rating.HasValue)
                      .Select(x => (double)x.Rating!.Value).DefaultIfEmpty(0).Average();
        return new StrategyAnalytics(s, t.Count, w, l, wr, pl, Math.Round(rr, 2), Math.Round(ar, 1));
    }

    private static SessionAnalytics MakeSession(TradingSession ses, List<TradeEntry> t)
    {
        int w = t.Count(x => x.Result == TradeResult.Profit);
        int l = t.Count(x => x.Result == TradeResult.Loss);
        int c = w + l + t.Count(x => x.Result == TradeResult.BreakEven);
        decimal pl = t.Where(x => x.ProfitLoss.HasValue).Sum(x => x.ProfitLoss!.Value);
        double  wr = c > 0 ? Math.Round((double)w / c * 100, 1) : 0;
        string  dn = ses switch {
            TradingSession.Asian         => "Asiática",
            TradingSession.London        => "Londres",
            TradingSession.NewYork       => "Nueva York",
            TradingSession.Sydney        => "Sydney",
            TradingSession.LondonNewYork => "Londres / NY",
            _                            => ses.ToString()
        };
        return new SessionAnalytics(dn, t.Count, w, l, wr, pl);
    }

    private static DirectionAnalytics MakeDirection(TradeDirection dir, List<TradeEntry> t)
    {
        int w = t.Count(x => x.Result == TradeResult.Profit);
        int l = t.Count(x => x.Result == TradeResult.Loss);
        int c = w + l + t.Count(x => x.Result == TradeResult.BreakEven);
        decimal pl = t.Where(x => x.ProfitLoss.HasValue).Sum(x => x.ProfitLoss!.Value);
        double  wr = c > 0 ? Math.Round((double)w / c * 100, 1) : 0;
        string  lb = dir == TradeDirection.Long ? "Long ▲" : "Short ▼";
        return new DirectionAnalytics(lb, dir == TradeDirection.Long ? 0 : 1, t.Count, w, l, wr, pl);
    }

    private static MonthlyAnalytics MakeMonth(int year, int month, List<TradeEntry> t)
    {
        int w = t.Count(x => x.Result == TradeResult.Profit);
        int l = t.Count(x => x.Result == TradeResult.Loss);
        int c = w + l + t.Count(x => x.Result == TradeResult.BreakEven);
        decimal pl = t.Where(x => x.ProfitLoss.HasValue).Sum(x => x.ProfitLoss!.Value);
        double  wr = c > 0 ? Math.Round((double)w / c * 100, 1) : 0;
        var     ci = CultureInfo.GetCultureInfo("es-CO");
        string  lb = new DateTime(year, month, 1).ToString("MMMM yyyy", ci);
        lb = char.ToUpper(lb[0]) + lb[1..];
        return new MonthlyAnalytics(lb, year * 100 + month, t.Count, w, l, wr, pl);
    }

    private void ResetKpis()
    {
        TotalTrades = WinningTrades = LosingTrades = BreakEvenTrades = OpenTrades
            = MaxWinStreak = MaxLossStreak = 0;
        WinRatePct = 0;
        WinRateDisplay = TotalPLDisplay = GrossProfitDisplay = GrossLossDisplay =
            ProfitFactorDisplay = AvgPLDisplay = AvgRRDisplay =
            BestTradeDisplay = WorstTradeDisplay = AvgRatingDisplay = ExpectancyDisplay = "—";
        TotalPLPositive = AvgPLPositive = ExpectancyPositive = false;
        BySymbol.Clear(); ByStrategy.Clear(); BySession.Clear();
        ByDirection.Clear(); ByMonth.Clear();
        MonthlyChart.Clear(); SessionChart.Clear();
        _allBySymbol.Clear(); _allByStrategy.Clear();
        SymbolCurrentPage = StrategyCurrentPage = 1;
        SymbolTotalPages  = StrategyTotalPages  = 1;
        OnPropertyChanged(nameof(WinPct));
        OnPropertyChanged(nameof(LossPct));
        OnPropertyChanged(nameof(BEPct));
    }

    // ── Excel builders ────────────────────────────────────────────────────

    private void BuildSummarySheet(XLWorkbook wb)
    {
        var ws = wb.Worksheets.Add("Resumen");
        ws.Cell(1, 1).Value = "ANÁLISIS DE TRADING";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 16;
        ws.Cell(1, 1).Style.Font.FontColor = XLColor.FromHtml("#00D4FF");
        ws.Range(1, 1, 1, 3).Merge();

        string period = $"{FilterDateFrom?.ToString("dd/MM/yyyy") ?? "inicio"} — {FilterDateTo?.ToString("dd/MM/yyyy") ?? "hoy"}";
        ws.Cell(2, 1).Value = $"Período: {period}   |   Generado: {DateTime.Now:dd/MM/yyyy HH:mm}";
        ws.Cell(2, 1).Style.Font.Italic = true;
        ws.Cell(2, 1).Style.Font.FontColor = XLColor.Gray;
        ws.Range(2, 1, 2, 3).Merge();

        int r = 4;
        void Row(string lbl, string val)
        {
            ws.Cell(r, 1).Value = lbl;
            ws.Cell(r, 1).Style.Font.Bold = true;
            ws.Cell(r, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A5F");
            ws.Cell(r, 1).Style.Font.FontColor = XLColor.White;
            ws.Cell(r, 2).Value = val;
            if (r % 2 == 0) ws.Cell(r, 2).Style.Fill.BackgroundColor = XLColor.FromHtml("#F5F7FA");
            r++;
        }
        Row("Total trades",            TotalTrades.ToString());
        Row("Ganadores",               WinningTrades.ToString());
        Row("Perdedores",              LosingTrades.ToString());
        Row("Break-even",              BreakEvenTrades.ToString());
        Row("Abiertos",                OpenTrades.ToString());
        Row("Win Rate",                WinRateDisplay);
        Row("P&L Total",               TotalPLDisplay);
        Row("Ganancia bruta",          GrossProfitDisplay);
        Row("Pérdida bruta",           GrossLossDisplay);
        Row("Factor de ganancia",      ProfitFactorDisplay);
        Row("P&L promedio",            AvgPLDisplay);
        Row("R:R promedio",            AvgRRDisplay);
        Row("Mejor trade",             BestTradeDisplay);
        Row("Peor trade",              WorstTradeDisplay);
        Row("Racha ganadora máx.",     MaxWinStreak.ToString());
        Row("Racha perdedora máx.",    MaxLossStreak.ToString());
        Row("Calificación promedio",   AvgRatingDisplay);
        Row("Expectancia",             ExpectancyDisplay);
        ws.Column(1).Width = 28;
        ws.Column(2).Width = 18;
    }

    private void BuildMonthlySheet(XLWorkbook wb)
    {
        var ws = wb.Worksheets.Add("Por Mes");
        WriteHdr(ws, "Mes", "Trades", "Ganadores", "Perdedores", "Win Rate %", "P&L Total");
        int r = 2;
        foreach (var m in ByMonth)
        {
            ws.Cell(r, 1).Value = m.Month;
            ws.Cell(r, 2).Value = m.Total;
            ws.Cell(r, 3).Value = m.Wins;
            ws.Cell(r, 4).Value = m.Losses;
            ws.Cell(r, 5).Value = m.WinRate;
            ws.Cell(r, 6).Value = (double)m.TotalPL;
            ColorPL(ws.Cell(r, 6), m.TotalPL);
            if (r % 2 == 0) ws.Row(r).Style.Fill.BackgroundColor = XLColor.FromHtml("#F5F7FA");
            r++;
        }
        ws.Columns().AdjustToContents(10, 40);
    }

    private void BuildSymbolSheet(XLWorkbook wb)
    {
        var ws = wb.Worksheets.Add("Por Símbolo");
        WriteHdr(ws, "Símbolo", "Trades", "Ganadores", "Perdedores", "Win Rate %", "P&L Total", "R:R Prom.");
        int r = 2;
        foreach (var s in BySymbol)
        {
            ws.Cell(r, 1).Value = s.Symbol;
            ws.Cell(r, 2).Value = s.Total;
            ws.Cell(r, 3).Value = s.Wins;
            ws.Cell(r, 4).Value = s.Losses;
            ws.Cell(r, 5).Value = s.WinRate;
            ws.Cell(r, 6).Value = (double)s.TotalPL;
            ws.Cell(r, 7).Value = s.AvgRR;
            ColorPL(ws.Cell(r, 6), s.TotalPL);
            if (r % 2 == 0) ws.Row(r).Style.Fill.BackgroundColor = XLColor.FromHtml("#F5F7FA");
            r++;
        }
        ws.Columns().AdjustToContents(10, 40);
    }

    private void BuildStrategySheet(XLWorkbook wb)
    {
        var ws = wb.Worksheets.Add("Por Estrategia");
        WriteHdr(ws, "Estrategia", "Trades", "Ganadores", "Perdedores",
                     "Win Rate %", "P&L Total", "R:R Prom.", "Cal. Prom.");
        int r = 2;
        foreach (var s in ByStrategy)
        {
            ws.Cell(r, 1).Value = s.Strategy;
            ws.Cell(r, 2).Value = s.Total;
            ws.Cell(r, 3).Value = s.Wins;
            ws.Cell(r, 4).Value = s.Losses;
            ws.Cell(r, 5).Value = s.WinRate;
            ws.Cell(r, 6).Value = (double)s.TotalPL;
            ws.Cell(r, 7).Value = s.AvgRR;
            if (s.AvgRating > 0) ws.Cell(r, 8).Value = s.AvgRating;
            ColorPL(ws.Cell(r, 6), s.TotalPL);
            if (r % 2 == 0) ws.Row(r).Style.Fill.BackgroundColor = XLColor.FromHtml("#F5F7FA");
            r++;
        }
        ws.Columns().AdjustToContents(10, 40);
    }

    private void BuildSessionSheet(XLWorkbook wb)
    {
        var ws = wb.Worksheets.Add("Por Sesión");
        WriteHdr(ws, "Sesión", "Trades", "Ganadores", "Perdedores", "Win Rate %", "P&L Total");
        int r = 2;
        foreach (var s in BySession)
        {
            ws.Cell(r, 1).Value = s.Session;
            ws.Cell(r, 2).Value = s.Total;
            ws.Cell(r, 3).Value = s.Wins;
            ws.Cell(r, 4).Value = s.Losses;
            ws.Cell(r, 5).Value = s.WinRate;
            ws.Cell(r, 6).Value = (double)s.TotalPL;
            ColorPL(ws.Cell(r, 6), s.TotalPL);
            if (r % 2 == 0) ws.Row(r).Style.Fill.BackgroundColor = XLColor.FromHtml("#F5F7FA");
            r++;
        }
        ws.Columns().AdjustToContents(10, 40);
    }

    private static void WriteHdr(IXLWorksheet ws, params string[] cols)
    {
        for (int c = 0; c < cols.Length; c++)
        {
            var cell = ws.Cell(1, c + 1);
            cell.Value = cols[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A5F");
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }
    }

    private static void ColorPL(IXLCell cell, decimal pl)
    {
        cell.Style.Font.FontColor = pl >= 0
            ? XLColor.FromHtml("#00C853")
            : XLColor.FromHtml("#FF5252");
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────────

public record SymbolAnalytics(
    string Symbol, int Total, int Wins, int Losses,
    double WinRate, decimal TotalPL, double AvgRR);

public record StrategyAnalytics(
    string Strategy, int Total, int Wins, int Losses,
    double WinRate, decimal TotalPL, double AvgRR, double AvgRating)
{
    public string AvgRatingDisplay => AvgRating > 0 ? $"{AvgRating:F1}" : "—";
}

public record SessionAnalytics(
    string Session, int Total, int Wins, int Losses,
    double WinRate, decimal TotalPL);

public record DirectionAnalytics(
    string Label, int SortOrder, int Total, int Wins, int Losses,
    double WinRate, decimal TotalPL);

public record MonthlyAnalytics(
    string Month, int SortKey, int Total, int Wins, int Losses,
    double WinRate, decimal TotalPL);

// ── DTOs de gráficos ──────────────────────────────────────────────────────

public record MonthlyChartPoint(string Month, decimal PL, bool IsPositive, double BarWidth);

public record SessionChartPoint(string Session, double WinRate, double BarWidth);
