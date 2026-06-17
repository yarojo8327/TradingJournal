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
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using TradingAccountEntity = Application.WPF.Models.Entities.TradingAccount;

namespace Application.WPF.ViewModels.Journal;

public partial class TradeJournalViewModel : BaseViewModel
{
    private readonly ITradeService            _tradeService;
    private readonly ITradingAccountService   _accountService;
    private readonly ITradingStrategyService  _strategyService;
    private readonly ISessionService          _sessionService;
    private readonly IDialogService           _dialogService;
    private readonly IJournalListService      _journalListService;
    private readonly ISymbolMappingService          _symbolMappingService;
    private readonly ILogger<TradeJournalViewModel> _logger;

    private int _tradeId;

    // ── Lista paginada ────────────────────────────────────────────────────

    private List<TradeEntry> _rawTrades = new();   // sin filtrar (para resumen de cuentas)
    private List<TradeEntry> _allTrades = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoTrades), nameof(PageInfo))]
    private int _totalCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageInfo), nameof(IsLastPage))]
    private int _totalPages = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageInfo), nameof(IsFirstPage), nameof(IsLastPage))]
    private int _currentPage = 1;

    public bool IsFirstPage => CurrentPage <= 1;
    public bool IsLastPage  => CurrentPage >= TotalPages;

    private const int PageSize = 25;

    [ObservableProperty]
    private ObservableCollection<TradeEntry> _pagedTrades = new();

    public bool HasNoTrades => TotalCount == 0;
    public string PageInfo  => $"Página {CurrentPage} de {TotalPages}  ·  {TotalCount} trades";

    // ── Resumen de cuentas ────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAccountSummaries))]
    private ObservableCollection<AccountSummaryItem> _accountSummaries = new();

    public bool HasAccountSummaries => AccountSummaries.Count > 0;

    // ── Panel de estadísticas ─────────────────────────────────────────────

    [ObservableProperty] private string _statsInitialCapital = "—";
    [ObservableProperty] private string _statsTotalOps       = "0";
    [ObservableProperty] private string _statsWon            = "0";
    [ObservableProperty] private string _statsLost           = "0";
    [ObservableProperty] private string _statsWinRate        = "—";
    [ObservableProperty] private string _statsNetPnl         = "—";
    [ObservableProperty] private bool   _statsNetPnlPositive = true;
    [ObservableProperty] private string _statsBestTrade      = "—";
    [ObservableProperty] private string _statsWorstTrade     = "—";
    [ObservableProperty] private string _statsAvgPnl         = "—";
    [ObservableProperty] private string _statsTotal          = "—";
    [ObservableProperty] private string _statsGainPct        = "—";
    [ObservableProperty] private bool   _statsGainPositive   = true;
    [ObservableProperty] private bool   _hasStats;

    /// <summary>Cumulative balance per closed trade, starting from initial capital.</summary>
    private List<double> _equityPoints = new();
    /// <summary>P&L value per closed trade (in trade order).</summary>
    private List<double> _pnlPoints    = new();
    public IReadOnlyList<double> EquityPoints => _equityPoints;
    public IReadOnlyList<double> PnlPoints    => _pnlPoints;
    public int StatsWonCount  { get; private set; }
    public int StatsLostCount { get; private set; }

    /// <summary>Fired when stats data is ready — triggers canvas redraw in code-behind.</summary>
    public event EventHandler? StatsUpdated;

    [ObservableProperty] private bool   _isFormVisible;
    [ObservableProperty] private bool   _isEditMode;
    [ObservableProperty] private string _formSectionTitle = string.Empty;
    [ObservableProperty] private string _generalError   = string.Empty;
    [ObservableProperty] private string _generalSuccess = string.Empty;

    // Filtros
    [ObservableProperty] private TradingAccountEntity? _filterAccount;
    [ObservableProperty] private string                _filterSymbol    = string.Empty;
    [ObservableProperty] private TradeResultOption?    _filterResult;
    [ObservableProperty] private TradingStrategy?      _filterStrategy;
    [ObservableProperty] private int?                  _filterRatingMin;
    [ObservableProperty] private int?                  _filterRatingMax;

    // ── Catálogos ─────────────────────────────────────────────────────────

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

    public IReadOnlyList<string> FilterSymbols { get; } = new List<string>
    {
        string.Empty,
        "EURUSD","GBPUSD","USDJPY","USDCHF","AUDUSD","NZDUSD","USDCAD","EURGBP",
        "EURJPY","EURCAD","EURAUD","EURNZD","EURCHF",
        "GBPJPY","GBPCHF","GBPCAD","GBPAUD","GBPNZD",
        "AUDJPY","CADJPY","NZDJPY","CHFJPY",
        "AUDCAD","AUDCHF","AUDNZD","CADCHF","NZDCAD","NZDCHF",
        "USDMXN","USDZAR","USDSGD","USDNOK","USDSEK","USDTRY",
        "XAUUSD","XAGUSD","USOIL","UKOIL",
        "BTCUSD","ETHUSD","BNBUSD","SOLUSD","XRPUSD","ADAUSD","AVAXUSD","DOTUSD","LINKUSD","MATICUSD",
        "US30","US500","NAS100","GER40","UK100","JP225","AUS200","HK50","FRA40","EU50"
    };

    public IReadOnlyList<string> Symbols { get; } = new List<string>
    {
        "EURUSD","GBPUSD","USDJPY","USDCHF","AUDUSD","NZDUSD","USDCAD","EURGBP",
        "EURJPY","EURCAD","EURAUD","EURNZD","EURCHF",
        "GBPJPY","GBPCHF","GBPCAD","GBPAUD","GBPNZD",
        "AUDJPY","CADJPY","NZDJPY","CHFJPY",
        "AUDCAD","AUDCHF","AUDNZD","CADCHF","NZDCAD","NZDCHF",
        "USDMXN","USDZAR","USDSGD","USDNOK","USDSEK","USDTRY",
        "XAUUSD","XAGUSD","USOIL","UKOIL",
        "BTCUSD","ETHUSD","BNBUSD","SOLUSD","XRPUSD","ADAUSD","AVAXUSD","DOTUSD","LINKUSD","MATICUSD",
        "US30","US500","NAS100","GER40","UK100","JP225","AUS200","HK50","FRA40","EU50"
    };

    public IReadOnlyList<TradeDirectionOption> Directions { get; } = new List<TradeDirectionOption>
    {
        new(TradeDirection.Long,  "Long  ▲"),
        new(TradeDirection.Short, "Short ▼")
    };

    public IReadOnlyList<TradeResultOption> Results { get; } = new List<TradeResultOption>
    {
        new(TradeResult.Open,      "Abierto"),
        new(TradeResult.Profit,    "Ganancia"),
        new(TradeResult.Loss,      "Pérdida"),
        new(TradeResult.BreakEven, "BreakEven")
    };

    public IReadOnlyList<TradeResultOption?> FilterResults { get; }

    public IReadOnlyList<SessionOption> Sessions { get; } = new List<SessionOption>
    {
        new(null,                          "— Ninguna —"),
        new(TradingSession.Asian,          "Asiática"),
        new(TradingSession.London,         "Londres"),
        new(TradingSession.NewYork,        "Nueva York"),
        new(TradingSession.Sydney,         "Sydney"),
        new(TradingSession.LondonNewYork,  "Londres / NY (overlap)")
    };

    public IReadOnlyList<TradingTypeOption> TradingTypes { get; } = new List<TradingTypeOption>
    {
        new(null,                      "— Ninguno —"),
        new(TradingType.Scalping,      "Scalping"),
        new(TradingType.Intraday,      "Intradía"),
        new(TradingType.Swing,         "Swing Trading"),
        new(TradingType.Position,      "Trading de posición")
    };

    [ObservableProperty] private ObservableCollection<string> _emotionalStateOptions = new();
    [ObservableProperty] private ObservableCollection<string> _mistakeTypeOptions    = new();

    public IReadOnlyList<string> TimeframeUnits { get; } = new[] { "s", "M", "H", "D", "W", "MN" };

    // ── Campos del formulario ─────────────────────────────────────────────

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "La cuenta es requerida.")]
    private TradingAccountEntity? _selectedAccount;

    [ObservableProperty] private TradingStrategy? _selectedStrategy;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "El símbolo es requerido.")]
    private string _symbol = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "La dirección es requerida.")]
    private TradeDirectionOption? _selectedDirection;

    [ObservableProperty] private DateTime  _entryDate = DateTime.Today;
    [ObservableProperty] private string    _entryTime = DateTime.Now.ToString("HH:mm");
    [ObservableProperty] private DateTime? _exitDate;
    [ObservableProperty] private string    _exitTime  = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "El precio de entrada es requerido.")]
    [CustomValidation(typeof(TradeJournalViewModel), nameof(ValidatePositiveDecimal))]
    private string _entryPriceText = string.Empty;

    [ObservableProperty]
    [CustomValidation(typeof(TradeJournalViewModel), nameof(ValidateOptionalPositiveDecimal))]
    private string _exitPriceText = string.Empty;

    [ObservableProperty]
    [CustomValidation(typeof(TradeJournalViewModel), nameof(ValidateOptionalPositiveDecimal))]
    private string _stopLossText = string.Empty;

    [ObservableProperty]
    [CustomValidation(typeof(TradeJournalViewModel), nameof(ValidateOptionalPositiveDecimal))]
    private string _takeProfitText = string.Empty;

    [ObservableProperty]
    [CustomValidation(typeof(TradeJournalViewModel), nameof(ValidateOptionalPositiveDecimal))]
    private string _positionSizeText = string.Empty;

    [ObservableProperty]
    [CustomValidation(typeof(TradeJournalViewModel), nameof(ValidateOptionalDecimal))]
    private string _riskAmountText = string.Empty;

    [ObservableProperty]
    [CustomValidation(typeof(TradeJournalViewModel), nameof(ValidateOptionalDecimal))]
    private string _profitLossText = string.Empty;

    [ObservableProperty]
    [CustomValidation(typeof(TradeJournalViewModel), nameof(ValidateOptionalPositiveDecimal))]
    private string _rrText = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "El resultado es requerido.")]
    private TradeResultOption? _selectedResult;

    [ObservableProperty] private int?               _selectedRating;
    [ObservableProperty] private TradingTypeOption? _selectedTradingType;
    [ObservableProperty] private SessionOption?     _selectedSession;
    [ObservableProperty] private string?            _selectedTimeframe;
    [ObservableProperty] private string             _timeframeValue = string.Empty;
    [ObservableProperty] private string?            _timeframeUnit  = "H";
    [ObservableProperty] private string?            _selectedEmotionalState;
    [ObservableProperty] private string?            _selectedMistakeType;
    [ObservableProperty] private string                _notes         = string.Empty;
    [ObservableProperty] private string                _screenshotUrl = string.Empty;

    // ── Constructor ───────────────────────────────────────────────────────

    public TradeJournalViewModel(
        ITradeService                  tradeService,
        ITradingAccountService         accountService,
        ITradingStrategyService        strategyService,
        ISessionService                sessionService,
        IDialogService                 dialogService,
        IJournalListService            journalListService,
        ISymbolMappingService          symbolMappingService,
        ILogger<TradeJournalViewModel> logger)
    {
        _tradeService         = tradeService;
        _accountService       = accountService;
        _strategyService      = strategyService;
        _sessionService       = sessionService;
        _dialogService        = dialogService;
        _journalListService   = journalListService;
        _symbolMappingService = symbolMappingService;
        _logger               = logger;
        Title            = "Bitácora de Trading";

        FilterResults = new List<TradeResultOption?> { null }
            .Concat(Results.Select(r => (TradeResultOption?)r))
            .ToList();

        _selectedResult = Results.FirstOrDefault(r => r.Value == TradeResult.Open);
    }

    public override async Task InitializeAsync()
    {
        var user = _sessionService.CurrentUser;
        if (user is null) return;

        await LoadCatalogsAsync(user.Id);
        await LoadTradesAsync(user.Id);
    }

    // ── Auto-cálculo del resultado ────────────────────────────────────────

    partial void OnExitPriceTextChanged(string value)    { AutoCalculateResult(); AutoCalculateRR(); }
    partial void OnEntryPriceTextChanged(string value)   { AutoCalculateResult(); AutoCalculateRR(); }
    partial void OnStopLossTextChanged(string value)     => AutoCalculateRR();
    partial void OnTakeProfitTextChanged(string value)   => AutoCalculateRR();
    partial void OnSelectedDirectionChanged(TradeDirectionOption? value) => AutoCalculateResult();
    partial void OnTimeframeValueChanged(string value)   => UpdateTimeframe();
    partial void OnTimeframeUnitChanged(string? value)   => UpdateTimeframe();

    private void UpdateTimeframe()
    {
        if (string.IsNullOrWhiteSpace(TimeframeValue))
            SelectedTimeframe = null;
        else
            SelectedTimeframe = $"{TimeframeValue.Trim()}{TimeframeUnit}";
    }

    private void AutoCalculateResult()
    {
        var entry = ParseDecimal(EntryPriceText);
        var exit  = ParseDecimal(ExitPriceText);

        if (entry is null || exit is null || SelectedDirection is null)
        {
            if (exit is null)
                SelectedResult = Results.FirstOrDefault(r => r.Value == TradeResult.Open);
            return;
        }

        var diff = exit.Value - entry.Value;
        TradeResult result;

        if (diff == 0m)
        {
            result = TradeResult.BreakEven;
        }
        else if (SelectedDirection.Value == TradeDirection.Long)
        {
            result = diff > 0 ? TradeResult.Profit : TradeResult.Loss;
        }
        else
        {
            result = diff < 0 ? TradeResult.Profit : TradeResult.Loss;
        }

        SelectedResult = Results.FirstOrDefault(r => r.Value == result);
    }

    private void AutoCalculateRR()
    {
        var entry = ParseDecimal(EntryPriceText);
        var sl    = ParseDecimal(StopLossText);
        var tp    = ParseDecimal(TakeProfitText);

        if (entry is null || sl is null || tp is null) return;
        var risk   = Math.Abs(entry.Value - sl.Value);
        var reward = Math.Abs(tp.Value - entry.Value);
        if (risk == 0m) return;

        var rr = Math.Round(reward / risk, 2);
        RrText = rr.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    // ── Carga de datos ────────────────────────────────────────────────────

    private async Task LoadCatalogsAsync(int userId)
    {
        var accs = await _accountService.GetAllByUserIdAsync(userId);
        Accounts = new ObservableCollection<TradingAccountEntity>(accs);

        if (FilterAccount is null && Accounts.Count > 0)
            FilterAccount = Accounts[0];

        var strats = await _strategyService.GetAllByUserIdAsync(userId);
        Strategies = new ObservableCollection<TradingStrategy>(strats);

        await _journalListService.EnsureDefaultsAsync(userId);
        await ReloadJournalListsAsync(userId);
    }

    public async Task ReloadJournalListsAsync(int userId)
    {
        var emotional = await _journalListService.GetNamesAsync(userId, JournalListCategory.EmotionalState);
        EmotionalStateOptions = new ObservableCollection<string>(emotional);

        var mistakes = await _journalListService.GetNamesAsync(userId, JournalListCategory.MistakeType);
        MistakeTypeOptions = new ObservableCollection<string>(mistakes);
    }

    private async Task LoadTradesAsync(int userId)
    {
        var list = await _tradeService.GetAllByUserIdAsync(userId);
        _rawTrades  = list.ToList();
        _allTrades  = ApplyFilters(_rawTrades).ToList();
        CurrentPage = 1;
        RefreshPage();
    }

    private void RefreshPage()
    {
        TotalCount = _allTrades.Count;
        TotalPages = Math.Max(1, (int)Math.Ceiling((double)TotalCount / PageSize));
        if (CurrentPage > TotalPages) CurrentPage = TotalPages;
        if (CurrentPage < 1) CurrentPage = 1;

        PagedTrades = new ObservableCollection<TradeEntry>(
            _allTrades.Skip((CurrentPage - 1) * PageSize).Take(PageSize));

        RefreshAccountSummaries();
        ComputeStats();
    }

    private void ComputeStats()
    {
        var closed = _allTrades
            .Where(t => t.Result is TradeResult.Profit or TradeResult.Loss or TradeResult.BreakEven)
            .OrderBy(t => t.ExitDate ?? t.EntryDate)
            .ToList();

        var wins   = closed.Where(t => t.Result == TradeResult.Profit).ToList();
        var losses = closed.Where(t => t.Result == TradeResult.Loss).ToList();

        StatsWonCount  = wins.Count;
        StatsLostCount = losses.Count;
        HasStats       = _allTrades.Count > 0;

        if (!HasStats)
        {
            StatsUpdated?.Invoke(this, EventArgs.Empty);
            return;
        }

        // ── KPIs ──────────────────────────────────────────────────────────

        var initial = FilterAccount?.InitialCapital ?? 0m;
        StatsInitialCapital = initial > 0 ? $"$ {initial:N2}" : "—";
        StatsTotalOps       = _allTrades.Count.ToString();
        StatsWon            = wins.Count.ToString();
        StatsLost           = losses.Count.ToString();

        var wr = closed.Count > 0 ? (double)wins.Count / closed.Count : 0;
        StatsWinRate = closed.Count > 0 ? $"{wr:P0}" : "—";

        var netPnl = _allTrades.Where(t => t.ProfitLoss.HasValue).Sum(t => t.ProfitLoss!.Value);
        StatsNetPnlPositive = netPnl >= 0;
        StatsNetPnl = netPnl >= 0 ? $"$ {netPnl:N2}" : $"-$ {Math.Abs(netPnl):N2}";

        var pnlList = closed.Where(t => t.ProfitLoss.HasValue).Select(t => t.ProfitLoss!.Value).ToList();
        StatsBestTrade  = pnlList.Count > 0 ? $"$ {pnlList.Max():N2}"                 : "—";
        StatsWorstTrade = pnlList.Count > 0 ? $"-$ {Math.Abs(pnlList.Min()):N2}"      : "—";
        StatsAvgPnl     = pnlList.Count > 0 ? $"$ {pnlList.Average():N2}"             : "—";

        var totalBalance = initial + netPnl;
        StatsTotal     = $"$ {totalBalance:N2}";

        var gainPct = initial > 0 ? (double)(netPnl / initial) * 100 : 0;
        StatsGainPositive = gainPct >= 0;
        StatsGainPct      = initial > 0 ? $"{gainPct:F2} %" : "—";

        // ── Chart data ────────────────────────────────────────────────────

        _equityPoints = new List<double>();
        _pnlPoints    = new List<double>();

        double cum = initial > 0 ? (double)initial : 0;
        if (cum > 0) _equityPoints.Add(cum);   // starting point

        foreach (var t in closed.Where(t => t.ProfitLoss.HasValue))
        {
            var pnl = (double)t.ProfitLoss!.Value;
            cum += pnl;
            _equityPoints.Add(cum);
            _pnlPoints.Add(pnl);
        }

        StatsUpdated?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshAccountSummaries()
    {
        // Si hay filtro de cuenta activo, mostrar solo esa cuenta; si no, todas las cuentas.
        var tradesToSummarize = FilterAccount is not null
            ? _rawTrades.Where(t => t.AccountId == FilterAccount.Id)
            : (IEnumerable<TradeEntry>)_rawTrades;

        var summaries = tradesToSummarize
            .Where(t => t.Account is not null)
            .GroupBy(t => t.AccountId)
            .Select(g =>
            {
                var account = Accounts.FirstOrDefault(a => a.Id == g.Key);
                if (account is null) return null;

                var totalPL = g.Where(t => t.ProfitLoss.HasValue).Sum(t => t.ProfitLoss!.Value);
                var initial = account.InitialCapital;
                var pct     = initial > 0 ? Math.Round((double)(totalPL / initial * 100m), 2) : 0.0;

                return new AccountSummaryItem(
                    AccountName:    account.ToString(),
                    Currency:       account.BaseCurrency,
                    InitialCapital: initial,
                    TotalPL:        totalPL,
                    PctChange:      pct,
                    IsPositive:     totalPL >= 0);
            })
            .OfType<AccountSummaryItem>()
            .ToList();

        AccountSummaries = new ObservableCollection<AccountSummaryItem>(summaries);
    }

    private IEnumerable<TradeEntry> ApplyFilters(IEnumerable<TradeEntry> list)
    {
        if (FilterAccount is not null)
            list = list.Where(t => t.AccountId == FilterAccount.Id);
        if (!string.IsNullOrWhiteSpace(FilterSymbol))
            list = list.Where(t => t.Symbol.Contains(FilterSymbol.Trim(), StringComparison.OrdinalIgnoreCase));
        if (FilterResult is not null)
            list = list.Where(t => t.Result == FilterResult.Value);
        if (FilterStrategy is not null)
            list = list.Where(t => t.StrategyId == FilterStrategy.Id);
        if (FilterRatingMin.HasValue)
            list = list.Where(t => t.Rating.HasValue && t.Rating.Value >= FilterRatingMin.Value);
        if (FilterRatingMax.HasValue)
            list = list.Where(t => t.Rating.HasValue && t.Rating.Value <= FilterRatingMax.Value);
        return list;
    }

    // ── Comandos ──────────────────────────────────────────────────────────

    [RelayCommand]
    private void NewTrade()
    {
        ClearForm();
        IsEditMode       = false;
        IsFormVisible    = true;
        FormSectionTitle = "Registrar nuevo trade";
        GeneralError     = GeneralSuccess = string.Empty;

        if (Accounts.Count == 1)
            SelectedAccount = Accounts[0];
    }

    [RelayCommand]
    private void EditTrade(TradeEntry trade)
    {
        ClearForm();
        _tradeId         = trade.Id;
        IsEditMode       = true;
        IsFormVisible    = true;
        FormSectionTitle = "Editar trade";
        GeneralError     = GeneralSuccess = string.Empty;

        SelectedAccount      = Accounts.FirstOrDefault(a => a.Id == trade.AccountId);
        SelectedStrategy     = Strategies.FirstOrDefault(s => s.Id == trade.StrategyId);
        Symbol               = trade.Symbol;
        SelectedDirection    = Directions.FirstOrDefault(d => d.Value == trade.Direction);
        EntryDate            = trade.EntryDate.Date;
        EntryTime            = trade.EntryDate.ToString("HH:mm");
        ExitDate             = trade.ExitDate?.Date;
        ExitTime             = trade.ExitDate?.ToString("HH:mm") ?? string.Empty;
        EntryPriceText       = trade.EntryPrice.ToString("G", Ic);
        ExitPriceText        = FormatDecimal(trade.ExitPrice);
        StopLossText         = FormatDecimal(trade.StopLoss);
        TakeProfitText       = FormatDecimal(trade.TakeProfit);
        PositionSizeText     = FormatDecimal(trade.PositionSizeLots);
        RiskAmountText       = FormatDecimal(trade.RiskAmount);
        ProfitLossText       = FormatDecimal(trade.ProfitLoss);
        RrText               = string.Empty;
        SelectedResult       = Results.FirstOrDefault(r => r.Value == trade.Result);
        SelectedRating       = trade.Rating;
        SelectedTradingType    = TradingTypes.FirstOrDefault(t => t.Value == trade.TradingType);
        SelectedSession        = Sessions.FirstOrDefault(s => s.Value == trade.Session);
        ParseTimeframe(trade.Timeframe);
        SelectedEmotionalState = trade.EmotionalState;
        SelectedMistakeType    = trade.MistakeType;
        Notes                = trade.Notes ?? string.Empty;
        ScreenshotUrl        = trade.ScreenshotUrl ?? string.Empty;

        // Si los tres precios están presentes, recalcula R:R desde los valores reales
        AutoCalculateRR();
        // Si no se pudo calcular (falta SL o TP), restaurar el valor guardado
        if (string.IsNullOrEmpty(RrText))
            RrText = FormatDecimal(trade.RiskRewardRatio);
    }

    [RelayCommand]
    private void ViewTrade(TradeEntry trade)
    {
        TradeToView = trade;
    }

    [ObservableProperty] private TradeEntry? _tradeToView;

    [RelayCommand]
    private void Cancel()
    {
        ClearForm();
        IsFormVisible  = false;
        GeneralError   = GeneralSuccess = string.Empty;
    }

    [RelayCommand]
    private async Task DeleteTradeAsync(TradeEntry trade)
    {
        var confirmed = _dialogService.ShowConfirmation(
            $"¿Eliminar la operación {trade.Symbol} del {(trade.ExitDate ?? trade.EntryDate):dd/MM/yyyy}?\nEsta acción no se puede deshacer.",
            "Confirmar eliminación");

        if (!confirmed) return;

        try
        {
            await _tradeService.DeleteAsync(trade.Id);
            var user = _sessionService.CurrentUser;
            if (user is not null) await LoadTradesAsync(user.Id);
            GeneralSuccess = $"Operación {trade.Symbol} eliminada correctamente.";
            GeneralError   = string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting trade {Id}", trade.Id);
            GeneralError   = "Error al eliminar la operación.";
            GeneralSuccess = string.Empty;
        }
    }

    [RelayCommand]
    private async Task ApplyFilterAsync()
    {
        var user = _sessionService.CurrentUser;
        if (user is not null) await LoadTradesAsync(user.Id);
    }

    [RelayCommand]
    private void NextPage()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
            RefreshPage();
        }
    }

    [RelayCommand]
    private void PreviousPage()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
            RefreshPage();
        }
    }

    [RelayCommand]
    private void OpenScreenshot(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not open URL {Url}", url);
        }
    }

    [RelayCommand]
    private void ExportToExcel()
    {
        if (!_allTrades.Any())
        {
            GeneralError   = "No hay trades para exportar.";
            GeneralSuccess = string.Empty;
            return;
        }

        var dlg = new SaveFileDialog
        {
            Title    = "Exportar Bitácora de Trading",
            Filter   = "Excel (*.xlsx)|*.xlsx",
            FileName = $"bitacora_{DateTime.Today:yyyyMMdd}.xlsx"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Trades");

            string[] headers = {
                "Fecha", "Símbolo", "Dirección", "Cuenta", "Estrategia",
                "Entrada", "Salida", "SL", "TP", "Lotes",
                "P&L", "R:R", "Resultado", "Sesión", "Calificación",
                "Estado emocional", "Error", "Notas", "URL gráfico"
            };
            for (int c = 0; c < headers.Length; c++)
                ws.Cell(1, c + 1).Value = headers[c];

            var hdr = ws.Range(1, 1, 1, headers.Length);
            hdr.Style.Font.Bold            = true;
            hdr.Style.Font.FontColor       = XLColor.White;
            hdr.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A5F");
            hdr.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            int row = 2;
            foreach (var t in _allTrades)
            {
                ws.Cell(row, 1).Value  = t.EntryDate.ToString("dd/MM/yyyy HH:mm");
                ws.Cell(row, 2).Value  = t.Symbol;
                ws.Cell(row, 3).Value  = t.Direction == TradeDirection.Long ? "Long ▲" : "Short ▼";
                ws.Cell(row, 4).Value  = t.Account?.Broker ?? string.Empty;
                ws.Cell(row, 5).Value  = t.Strategy?.Title ?? string.Empty;
                ws.Cell(row, 6).Value  = (double)t.EntryPrice;
                if (t.ExitPrice.HasValue)        ws.Cell(row, 7).Value  = (double)t.ExitPrice.Value;
                if (t.StopLoss.HasValue)         ws.Cell(row, 8).Value  = (double)t.StopLoss.Value;
                if (t.TakeProfit.HasValue)       ws.Cell(row, 9).Value  = (double)t.TakeProfit.Value;
                if (t.PositionSizeLots.HasValue) ws.Cell(row, 10).Value = (double)t.PositionSizeLots.Value;
                if (t.ProfitLoss.HasValue)       ws.Cell(row, 11).Value = (double)t.ProfitLoss.Value;
                if (t.RiskRewardRatio.HasValue)  ws.Cell(row, 12).Value = (double)t.RiskRewardRatio.Value;
                ws.Cell(row, 13).Value = t.Result switch
                {
                    TradeResult.Profit    => "Ganancia",
                    TradeResult.Loss      => "Pérdida",
                    TradeResult.BreakEven => "BreakEven",
                    _                     => "Abierto"
                };
                ws.Cell(row, 14).Value = t.Session switch
                {
                    TradingSession.Asian         => "Asiática",
                    TradingSession.London        => "Londres",
                    TradingSession.NewYork       => "Nueva York",
                    TradingSession.Sydney        => "Sydney",
                    TradingSession.LondonNewYork => "Londres / NY",
                    _                            => string.Empty
                };
                if (t.Rating.HasValue) ws.Cell(row, 15).Value = t.Rating.Value;
                ws.Cell(row, 16).Value = t.EmotionalState ?? string.Empty;
                ws.Cell(row, 17).Value = t.MistakeType ?? string.Empty;
                ws.Cell(row, 18).Value = t.Notes ?? string.Empty;
                ws.Cell(row, 19).Value = t.ScreenshotUrl ?? string.Empty;

                if (t.ProfitLoss.HasValue)
                {
                    var plCell = ws.Cell(row, 11);
                    if (t.Result == TradeResult.Profit)
                        plCell.Style.Font.FontColor = XLColor.FromHtml("#00C853");
                    else if (t.Result == TradeResult.Loss)
                        plCell.Style.Font.FontColor = XLColor.FromHtml("#FF5252");
                }

                if (row % 2 == 0)
                    ws.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#F0F4F8");

                row++;
            }

            ws.Range(1, 1, row - 1, headers.Length).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range(1, 1, row - 1, headers.Length).Style.Border.InsideBorder  = XLBorderStyleValues.Hair;
            ws.Columns().AdjustToContents(8, 60);

            wb.SaveAs(dlg.FileName);

            GeneralSuccess = $"Exportado: {System.IO.Path.GetFileName(dlg.FileName)}  ({_allTrades.Count} trades)";
            GeneralError   = string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting to Excel");
            GeneralError   = "Error al exportar el archivo Excel.";
            GeneralSuccess = string.Empty;
        }
    }

    // ── Importar desde Excel (MT5 History Report) ─────────────────────────

    [RelayCommand]
    private async Task ImportFromExcelAsync()
    {
        if (FilterAccount is null)
        {
            GeneralError   = "Seleccione una cuenta antes de importar.";
            GeneralSuccess = string.Empty;
            return;
        }

        var dlg = new OpenFileDialog
        {
            Title  = "Importar historial MT5 — seleccione el archivo Excel",
            Filter = "Excel (*.xlsx)|*.xlsx"
        };
        if (dlg.ShowDialog() != true) return;

        var user = _sessionService.CurrentUser;
        if (user is null) return;

        IsBusy       = true;
        GeneralError = GeneralSuccess = string.Empty;

        try
        {
            // Load user-defined symbol map and parse in background thread
            var symbolMap = await _symbolMappingService.GetMappingDictionaryAsync();
            var parsed = await Task.Run(() => Mt5ReportParser.Parse(dlg.FileName, FilterAccount.Id, symbolMap));

            if (parsed.Count == 0)
            {
                GeneralError = "No se encontraron operaciones válidas en el archivo.";
                return;
            }

            // Deduplication: load existing trades for the account
            var existing = await _tradeService.GetAllByAccountIdAsync(FilterAccount.Id);
            var existingKeys = existing
                .Select(t => $"{t.EntryDate:yyyyMMddHHmm}|{t.Symbol}|{t.EntryPrice:F5}")
                .ToHashSet(StringComparer.Ordinal);

            int imported = 0, skipped = 0;
            foreach (var data in parsed)
            {
                var key = $"{data.EntryDate:yyyyMMddHHmm}|{data.Symbol}|{data.EntryPrice:F5}";
                if (existingKeys.Contains(key)) { skipped++; continue; }
                await _tradeService.CreateAsync(data);
                imported++;
            }

            await LoadTradesAsync(user.Id);

            GeneralSuccess = imported > 0
                ? $"✓  {imported} operación(es) importada(s) correctamente" +
                  (skipped > 0 ? $"  ·  {skipped} omitida(s) (ya existían)" : string.Empty) + "."
                : $"No se importaron operaciones nuevas — {skipped} ya existían en la bitácora.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing MT5 Excel report");
            GeneralError = $"Error al importar: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ClearFilterAsync()
    {
        FilterAccount   = null;
        FilterSymbol    = string.Empty;
        FilterResult    = null;
        FilterStrategy  = null;
        FilterRatingMin = null;
        FilterRatingMax = null;
        var user = _sessionService.CurrentUser;
        if (user is not null) await LoadTradesAsync(user.Id);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        GeneralError = GeneralSuccess = string.Empty;
        ValidateFormFields();
        if (HasErrors) return;

        if (!TryParseEntryDateTime(out var entryDt))
        {
            GeneralError = "La fecha/hora de entrada no es válida.";
            return;
        }

        DateTime? exitDt = null;
        if (ExitDate.HasValue)
        {
            if (!TryParseExitDateTime(out var ed))
            {
                GeneralError = "La hora de salida no es válida.";
                return;
            }
            exitDt = ed;
        }

        var data = new TradeEntryData(
            AccountId:        SelectedAccount!.Id,
            StrategyId:       SelectedStrategy?.Id,
            Symbol:           Symbol,
            Direction:        SelectedDirection!.Value,
            EntryDate:        entryDt,
            ExitDate:         exitDt,
            EntryPrice:       ParseDecimal(EntryPriceText)!.Value,
            ExitPrice:        ParseDecimal(ExitPriceText),
            StopLoss:         ParseDecimal(StopLossText),
            TakeProfit:       ParseDecimal(TakeProfitText),
            PositionSizeLots: ParseDecimal(PositionSizeText),
            RiskAmount:       ParseDecimal(RiskAmountText),
            ProfitLoss:       ParseDecimal(ProfitLossText),
            PipsResult:       null,
            RiskRewardRatio:  ParseDecimal(RrText),
            Result:           SelectedResult!.Value,
            Session:          SelectedSession?.Value,
            Timeframe:        SelectedTimeframe,
            TradingType:      SelectedTradingType?.Value,
            SetupQuality:     null,
            ConfluencesCount: null,
            IsFalseBreakout:  false,
            Rating:           SelectedRating,
            EmotionalState:   SelectedEmotionalState,
            MistakeType:      SelectedMistakeType,
            Notes:            Notes,
            ScreenshotUrl:    ScreenshotUrl
        );

        var user = _sessionService.CurrentUser;
        if (user is null) return;

        IsBusy = true;
        try
        {
            if (IsEditMode)
            {
                await _tradeService.UpdateAsync(_tradeId, data);
                GeneralSuccess = "Trade actualizado correctamente.";
            }
            else
            {
                await _tradeService.CreateAsync(data);
                GeneralSuccess = "Trade registrado correctamente.";
            }

            await LoadTradesAsync(user.Id);
            IsFormVisible = false;
            ClearForm();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving trade");
            GeneralError = "Ocurrió un error al guardar el trade.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ParseTimeframe(string? tf)
    {
        if (string.IsNullOrWhiteSpace(tf)) { TimeframeValue = string.Empty; TimeframeUnit = "H"; return; }
        var match = System.Text.RegularExpressions.Regex.Match(tf.Trim(), @"^(\d+)([a-zA-Z]+)$");
        if (match.Success)
        {
            TimeframeValue = match.Groups[1].Value;
            TimeframeUnit  = TimeframeUnits.Contains(match.Groups[2].Value) ? match.Groups[2].Value : "H";
        }
        else
        {
            TimeframeValue = tf;
            TimeframeUnit  = "H";
        }
        SelectedTimeframe = tf;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static readonly System.Globalization.CultureInfo Ic =
        System.Globalization.CultureInfo.InvariantCulture;

    private bool TryParseEntryDateTime(out DateTime result)
    {
        result = default;
        if (!TimeSpan.TryParse(EntryTime.Trim(), out var ts)) return false;
        result = EntryDate.Date + ts;
        return true;
    }

    private bool TryParseExitDateTime(out DateTime result)
    {
        result = default;
        var timeStr = string.IsNullOrWhiteSpace(ExitTime) ? "00:00" : ExitTime.Trim();
        if (!TimeSpan.TryParse(timeStr, out var ts)) return false;
        result = ExitDate!.Value.Date + ts;
        return true;
    }

    private static decimal? ParseDecimal(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        return decimal.TryParse(text, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    private static string FormatDecimal(decimal? value) =>
        value.HasValue ? value.Value.ToString("G", System.Globalization.CultureInfo.InvariantCulture) : string.Empty;

    private void ValidateFormFields()
    {
        ValidateProperty(SelectedAccount,   nameof(SelectedAccount));
        ValidateProperty(Symbol,            nameof(Symbol));
        ValidateProperty(SelectedDirection, nameof(SelectedDirection));
        ValidateProperty(EntryPriceText,    nameof(EntryPriceText));
        ValidateProperty(SelectedResult,    nameof(SelectedResult));
    }

    private void ClearForm()
    {
        _tradeId               = 0;
        SelectedAccount        = null;
        SelectedStrategy       = null;
        Symbol                 = string.Empty;
        SelectedDirection      = null;
        EntryDate              = DateTime.Today;
        EntryTime              = DateTime.Now.ToString("HH:mm");
        ExitDate               = null;
        ExitTime               = string.Empty;
        EntryPriceText         = string.Empty;
        ExitPriceText          = string.Empty;
        StopLossText           = string.Empty;
        TakeProfitText         = string.Empty;
        PositionSizeText       = string.Empty;
        RiskAmountText         = string.Empty;
        ProfitLossText         = string.Empty;
        RrText                 = string.Empty;
        SelectedResult         = Results.FirstOrDefault(r => r.Value == TradeResult.Open);
        SelectedRating         = null;
        SelectedTradingType    = null;
        SelectedSession        = null;
        TimeframeValue         = string.Empty;
        TimeframeUnit          = "H";
        SelectedTimeframe      = null;
        SelectedEmotionalState = null;
        SelectedMistakeType    = null;
        Notes                  = string.Empty;
        ScreenshotUrl          = string.Empty;
        ClearErrors();
    }

    // ── Validaciones estáticas ────────────────────────────────────────────

    public static ValidationResult? ValidatePositiveDecimal(string value, ValidationContext _)
    {
        if (string.IsNullOrWhiteSpace(value)) return ValidationResult.Success;
        if (!decimal.TryParse(value, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var d) || d <= 0)
            return new ValidationResult("Debe ser un número mayor que cero.");
        return ValidationResult.Success;
    }

    public static ValidationResult? ValidateOptionalPositiveDecimal(string? value, ValidationContext _)
    {
        if (string.IsNullOrWhiteSpace(value)) return ValidationResult.Success;
        if (!decimal.TryParse(value, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var d) || d <= 0)
            return new ValidationResult("Debe ser un número mayor que cero.");
        return ValidationResult.Success;
    }

    public static ValidationResult? ValidateOptionalDecimal(string? value, ValidationContext _)
    {
        if (string.IsNullOrWhiteSpace(value)) return ValidationResult.Success;
        if (!decimal.TryParse(value, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out decimal __))
            return new ValidationResult("Debe ser un número válido.");
        return ValidationResult.Success;
    }
}

// ── Tipos auxiliares ──────────────────────────────────────────────────────

public record TradeDirectionOption(TradeDirection Value, string Display)
{
    public override string ToString() => Display;
}

public record TradeResultOption(TradeResult Value, string Display)
{
    public override string ToString() => Display;
}

public record SessionOption(TradingSession? Value, string Display)
{
    public override string ToString() => Display;
}

public record TradingTypeOption(TradingType? Value, string Display)
{
    public override string ToString() => Display;
}

// ── Resumen de cuenta ─────────────────────────────────────────────────────

public record AccountSummaryItem(
    string  AccountName,
    string  Currency,
    decimal InitialCapital,
    decimal TotalPL,
    double  PctChange,
    bool    IsPositive)
{
    public decimal CurrentCapital => InitialCapital + TotalPL;

    public string InitialCapitalDisplay => $"{InitialCapital:N2} {Currency}";
    public string CurrentCapitalDisplay => $"{CurrentCapital:N2} {Currency}";
    public string TotalPLDisplay        => TotalPL >= 0
        ? $"+{TotalPL:N2} {Currency}"
        : $"{TotalPL:N2} {Currency}";
    public string PctChangeDisplay      => PctChange >= 0
        ? $"+{PctChange:F2} %"
        : $"{PctChange:F2} %";
}
