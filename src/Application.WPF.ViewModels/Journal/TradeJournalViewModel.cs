using Application.WPF.Common.ViewModels;
using Application.WPF.Models.Entities;
using Application.WPF.Models.Enums;
using Application.WPF.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using TradingAccountEntity = Application.WPF.Models.Entities.TradingAccount;

namespace Application.WPF.ViewModels.Journal;

public partial class TradeJournalViewModel : BaseViewModel
{
    private readonly ITradeService            _tradeService;
    private readonly ITradingAccountService   _accountService;
    private readonly ITradingStrategyService  _strategyService;
    private readonly ISessionService          _sessionService;
    private readonly ILogger<TradeJournalViewModel> _logger;

    private int _tradeId;

    // ── Lista ─────────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoTrades))]
    private ObservableCollection<TradeEntry> _trades = new();

    [ObservableProperty] private bool   _isFormVisible;
    [ObservableProperty] private bool   _isEditMode;
    [ObservableProperty] private string _formSectionTitle = string.Empty;
    [ObservableProperty] private string _generalError   = string.Empty;
    [ObservableProperty] private string _generalSuccess = string.Empty;

    // Filtros
    [ObservableProperty] private TradingAccountEntity? _filterAccount;
    [ObservableProperty] private string _filterSymbol = string.Empty;
    [ObservableProperty] private TradeResultOption?    _filterResult;

    public bool HasNoTrades => !Trades.Any();

    // ── Catálogos ─────────────────────────────────────────────────────────

    [ObservableProperty] private ObservableCollection<TradingAccountEntity> _accounts = new();
    [ObservableProperty] private ObservableCollection<TradingStrategy>      _strategies = new();

    public IReadOnlyList<string> Symbols { get; } = new List<string>
    {
        // Forex Majors
        "EURUSD","GBPUSD","USDJPY","USDCHF","AUDUSD","NZDUSD","USDCAD",
        // Forex Crosses
        "EURGBP","EURJPY","GBPJPY","EURCHF","AUDJPY","CADJPY",
        // Metales
        "XAUUSD","XAGUSD",
        // Cripto
        "BTCUSD","ETHUSD","BNBUSD","SOLUSD",
        // Índices
        "US30","US500","NAS100","GER40","UK100","JP225"
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
        new(null,                          "— Cualquiera —"),
        new(TradingSession.Asian,          "Asiática"),
        new(TradingSession.London,         "Londres"),
        new(TradingSession.NewYork,        "Nueva York"),
        new(TradingSession.Sydney,         "Sydney"),
        new(TradingSession.LondonNewYork,  "Londres / NY (overlap)")
    };

    public IReadOnlyList<EmotionalStateOption> EmotionalStates { get; } = new List<EmotionalStateOption>
    {
        new(null,                          "— Sin registro —"),
        new(EmotionalState.Calm,           "Calmado"),
        new(EmotionalState.Disciplined,    "Disciplinado"),
        new(EmotionalState.Confident,      "Confiado"),
        new(EmotionalState.Excited,        "Emocionado"),
        new(EmotionalState.Anxious,        "Ansioso"),
        new(EmotionalState.Fearful,        "Temeroso"),
        new(EmotionalState.FOMO,           "FOMO"),
        new(EmotionalState.Revenge,        "Venganza")
    };

    public IReadOnlyList<string> Timeframes { get; } = new List<string>
        { "1M","5M","15M","30M","1H","4H","D1","W1" };

    public IReadOnlyList<string?> MistakeTypes { get; } = new List<string?>
    {
        null,
        "FOMO — Entré tarde",
        "Revenge trading",
        "Tamaño excesivo de posición",
        "Ignoré la invalidación",
        "Salí demasiado pronto",
        "No respeté el SL",
        "Operé contra la tendencia",
        "Setup de baja calidad",
        "Gestión de riesgo incorrecta",
        "Otro"
    };

    public IReadOnlyList<int?> QualityOptions { get; } =
        new List<int?> { null, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

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

    [ObservableProperty] private DateTime  _entryDate  = DateTime.Today;
    [ObservableProperty] private string    _entryTime  = DateTime.Now.ToString("HH:mm");
    [ObservableProperty] private DateTime? _exitDate;
    [ObservableProperty] private string    _exitTime   = string.Empty;

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
    [CustomValidation(typeof(TradeJournalViewModel), nameof(ValidateOptionalDecimal))]
    private string _pipsResultText = string.Empty;

    [ObservableProperty]
    [CustomValidation(typeof(TradeJournalViewModel), nameof(ValidateOptionalPositiveDecimal))]
    private string _rrText = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "El resultado es requerido.")]
    private TradeResultOption? _selectedResult;

    [ObservableProperty] private SessionOption?        _selectedSession;
    [ObservableProperty] private string?               _selectedTimeframe;
    [ObservableProperty] private int?                  _selectedSetupQuality;
    [ObservableProperty] private int?                  _selectedConfluencesCount;
    [ObservableProperty] private bool                  _isFalseBreakout;
    [ObservableProperty] private EmotionalStateOption? _selectedEmotionalState;
    [ObservableProperty] private string?               _selectedMistakeType;
    [ObservableProperty] private string                _notes          = string.Empty;
    [ObservableProperty] private string                _screenshotUrl  = string.Empty;

    // ── Constructor ───────────────────────────────────────────────────────

    public TradeJournalViewModel(
        ITradeService                  tradeService,
        ITradingAccountService         accountService,
        ITradingStrategyService        strategyService,
        ISessionService                sessionService,
        ILogger<TradeJournalViewModel> logger)
    {
        _tradeService    = tradeService;
        _accountService  = accountService;
        _strategyService = strategyService;
        _sessionService  = sessionService;
        _logger          = logger;
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

    // ── Carga de datos ────────────────────────────────────────────────────

    private async Task LoadCatalogsAsync(int userId)
    {
        var accs = await _accountService.GetAllByUserIdAsync(userId);
        Accounts = new ObservableCollection<TradingAccountEntity>(accs);

        var strats = await _strategyService.GetAllByUserIdAsync(userId);
        Strategies = new ObservableCollection<TradingStrategy>(strats);
    }

    private async Task LoadTradesAsync(int userId)
    {
        var list = await _tradeService.GetAllByUserIdAsync(userId);
        var filtered = ApplyFilters(list);
        Trades = new ObservableCollection<TradeEntry>(filtered);
    }

    private IEnumerable<TradeEntry> ApplyFilters(IEnumerable<TradeEntry> list)
    {
        if (FilterAccount is not null)
            list = list.Where(t => t.AccountId == FilterAccount.Id);
        if (!string.IsNullOrWhiteSpace(FilterSymbol))
            list = list.Where(t => t.Symbol.Contains(FilterSymbol.Trim(), StringComparison.OrdinalIgnoreCase));
        if (FilterResult is not null)
            list = list.Where(t => t.Result == FilterResult.Value);
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

        // Pre-seleccionar la única cuenta si solo hay una
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
        EntryPriceText       = trade.EntryPrice.ToString("G", System.Globalization.CultureInfo.InvariantCulture);
        ExitPriceText        = FormatDecimal(trade.ExitPrice);
        StopLossText         = FormatDecimal(trade.StopLoss);
        TakeProfitText       = FormatDecimal(trade.TakeProfit);
        PositionSizeText     = FormatDecimal(trade.PositionSizeLots);
        RiskAmountText       = FormatDecimal(trade.RiskAmount);
        ProfitLossText       = FormatDecimal(trade.ProfitLoss);
        PipsResultText       = FormatDecimal(trade.PipsResult);
        RrText               = FormatDecimal(trade.RiskRewardRatio);
        SelectedResult       = Results.FirstOrDefault(r => r.Value == trade.Result);
        SelectedSession      = Sessions.FirstOrDefault(s => s.Value == trade.Session);
        SelectedTimeframe    = trade.Timeframe;
        SelectedSetupQuality  = trade.SetupQuality;
        SelectedConfluencesCount = trade.ConfluencesCount;
        IsFalseBreakout      = trade.IsFalseBreakout;
        SelectedEmotionalState = EmotionalStates.FirstOrDefault(e => e.Value == trade.EmotionalState);
        SelectedMistakeType  = trade.MistakeType;
        Notes                = trade.Notes ?? string.Empty;
        ScreenshotUrl        = trade.ScreenshotUrl ?? string.Empty;
    }

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
        try
        {
            await _tradeService.DeleteAsync(trade.Id);
            var user = _sessionService.CurrentUser;
            if (user is not null) await LoadTradesAsync(user.Id);
            GeneralSuccess = "Trade eliminado correctamente.";
            GeneralError   = string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting trade {Id}", trade.Id);
            GeneralError   = "Error al eliminar el trade.";
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
    private async Task ClearFilterAsync()
    {
        FilterAccount = null;
        FilterSymbol  = string.Empty;
        FilterResult  = null;
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
            PipsResult:       ParseDecimal(PipsResultText),
            RiskRewardRatio:  ParseDecimal(RrText),
            Result:           SelectedResult!.Value,
            Session:          SelectedSession?.Value,
            Timeframe:        SelectedTimeframe,
            SetupQuality:     SelectedSetupQuality,
            ConfluencesCount: SelectedConfluencesCount,
            IsFalseBreakout:  IsFalseBreakout,
            EmotionalState:   SelectedEmotionalState?.Value,
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

    // ── Helpers ───────────────────────────────────────────────────────────

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
        return decimal.TryParse(text,
            System.Globalization.NumberStyles.Any,
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
        PipsResultText         = string.Empty;
        RrText                 = string.Empty;
        SelectedResult         = Results.FirstOrDefault(r => r.Value == TradeResult.Open);
        SelectedSession        = null;
        SelectedTimeframe      = null;
        SelectedSetupQuality   = null;
        SelectedConfluencesCount = null;
        IsFalseBreakout        = false;
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

public record TradeDirectionOption(TradeDirection Value, string Display);
public record TradeResultOption(TradeResult Value, string Display);
public record SessionOption(TradingSession? Value, string Display);
public record EmotionalStateOption(EmotionalState? Value, string Display);
