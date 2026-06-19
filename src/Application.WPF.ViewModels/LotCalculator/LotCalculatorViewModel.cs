using Application.WPF.Common.ViewModels;
using Application.WPF.Models.Entities;
using Application.WPF.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Globalization;
using TradingAccountEntity = Application.WPF.Models.Entities.TradingAccount;

namespace Application.WPF.ViewModels.LotCalculator;

/// <summary>
/// Calculadora de lotaje basada en gestión de riesgo (HU-TRD-001).
/// Calcula el tamaño de posición recomendado a partir del capital, % de riesgo
/// y distancia del Stop Loss, validando las reglas de negocio RN-001, RN-002, RN-005 y RN-006.
/// </summary>
public partial class LotCalculatorViewModel : BaseViewModel
{
    private readonly ILotCalculatorService     _calculatorService;
    private readonly ISymbolMappingService     _symbolMappingService;
    private readonly ITradingAccountService    _accountService;
    private readonly ISessionService           _sessionService;
    private readonly ILogger<LotCalculatorViewModel> _logger;

    private static readonly CultureInfo Ic = CultureInfo.InvariantCulture;

    public LotCalculatorViewModel(
        ILotCalculatorService            calculatorService,
        ISymbolMappingService            symbolMappingService,
        ITradingAccountService           accountService,
        ISessionService                  sessionService,
        ILogger<LotCalculatorViewModel>  logger)
    {
        _calculatorService    = calculatorService;
        _symbolMappingService = symbolMappingService;
        _accountService       = accountService;
        _sessionService       = sessionService;
        _logger               = logger;
        Title                 = "Calculadora de Lotaje";
    }

    // ── Catálogos ─────────────────────────────────────────────────────────

    [ObservableProperty] private ObservableCollection<TradingAccountEntity> _accounts = new();
    [ObservableProperty] private ObservableCollection<string>               _symbols  = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AccountCurrency))]
    private TradingAccountEntity? _selectedAccount;

    public string AccountCurrency => SelectedAccount?.BaseCurrency ?? "USD";

    [ObservableProperty] private string? _selectedSymbol;

    // ── Entradas ──────────────────────────────────────────────────────────

    [ObservableProperty] private string _capitalText     = string.Empty;
    [ObservableProperty] private string _riskPercentText = "1";
    [ObservableProperty] private string _entryPriceText  = string.Empty;
    [ObservableProperty] private string _stopLossText    = string.Empty;
    [ObservableProperty] private string _takeProfitText  = string.Empty;

    // ── Resultado ─────────────────────────────────────────────────────────

    [ObservableProperty] private bool    _hasResult;
    [ObservableProperty] private string  _lotSizeText      = "—";
    [ObservableProperty] private string  _riskAmountText   = "—";
    [ObservableProperty] private string  _riskRewardText   = "—";
    [ObservableProperty] private string  _errorMessage     = string.Empty;
    [ObservableProperty] private string  _warningMessage   = string.Empty;
    [ObservableProperty] private string  _generalSuccess   = string.Empty;

    [ObservableProperty] private ObservableCollection<LotCalculation> _history = new();

    // ── Inicialización ────────────────────────────────────────────────────

    public override async Task InitializeAsync()
    {
        var user = _sessionService.CurrentUser;
        if (user is null) return;

        var accounts = await _accountService.GetAllByUserIdAsync(user.Id);
        Accounts = new ObservableCollection<TradingAccountEntity>(accounts);
        if (SelectedAccount is null && Accounts.Count > 0)
            SelectedAccount = Accounts[0];

        await _symbolMappingService.EnsureDefaultsAsync();
        var canonicalNames = await _symbolMappingService.GetCanonicalNamesAsync();
        Symbols = new ObservableCollection<string>(canonicalNames);

        await LoadHistoryAsync(user.Id);
    }

    private async Task LoadHistoryAsync(int userId)
    {
        var history = await _calculatorService.GetHistoryAsync(userId, take: 10);
        History = new ObservableCollection<LotCalculation>(history);
    }

    // ── Recálculo automático (RN-004) ────────────────────────────────────

    partial void OnSelectedAccountChanged(TradingAccountEntity? value)
    {
        if (value is not null && string.IsNullOrWhiteSpace(CapitalText))
            CapitalText = value.InitialCapital.ToString("0.##", Ic);
        _ = RecalculateAsync();
    }

    partial void OnSelectedSymbolChanged(string? value)        => _ = RecalculateAsync();
    partial void OnCapitalTextChanged(string value)             => _ = RecalculateAsync();
    partial void OnRiskPercentTextChanged(string value)         => _ = RecalculateAsync();
    partial void OnEntryPriceTextChanged(string value)          => _ = RecalculateAsync();
    partial void OnStopLossTextChanged(string value)            => _ = RecalculateAsync();
    partial void OnTakeProfitTextChanged(string value)          => _ = RecalculateAsync();

    private LotCalculationResult? _lastResult;

    private async Task RecalculateAsync()
    {
        GeneralSuccess = string.Empty;
        HasResult       = false;
        ErrorMessage    = string.Empty;
        WarningMessage  = string.Empty;
        _lastResult     = null;

        if (string.IsNullOrWhiteSpace(SelectedSymbol)) return;

        var capital     = ParseDecimal(CapitalText);
        var riskPercent = ParseDecimal(RiskPercentText);
        var entryPrice  = ParseDecimal(EntryPriceText);
        var stopLoss    = ParseDecimal(StopLossText);
        var takeProfit  = ParseDecimal(TakeProfitText);

        if (capital is null || riskPercent is null || entryPrice is null || stopLoss is null) return;

        var user = _sessionService.CurrentUser;
        if (user is null) return;

        var request = new LotCalculationRequest(
            UserId:                 user.Id,
            AccountId:              SelectedAccount?.Id,
            Symbol:                 SelectedSymbol,
            Capital:                capital.Value,
            RiskPercent:            riskPercent.Value,
            EntryPrice:             entryPrice.Value,
            StopLoss:               stopLoss.Value,
            TakeProfit:             takeProfit,
            AccountCurrency:        AccountCurrency,
            MaxRiskPercentPerTrade: SelectedAccount?.MaxRiskPercentPerTrade);

        var result = await _calculatorService.CalculateAsync(request);
        _lastResult = result;

        if (!result.Success)
        {
            ErrorMessage = result.ErrorMessage ?? "No se pudo calcular el lotaje.";
            return;
        }

        HasResult       = true;
        LotSizeText     = $"{result.LotSize:0.00} lotes";
        RiskAmountText  = $"{result.RiskAmount:N2} {AccountCurrency}";
        RiskRewardText  = result.RiskRewardRatio.HasValue ? $"1 : {result.RiskRewardRatio:0.00}" : "—";
        WarningMessage  = result.WarningMessage ?? string.Empty;
    }

    // ── Comandos ──────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task SaveCalculationAsync()
    {
        var user = _sessionService.CurrentUser;
        if (user is null || _lastResult is null || !_lastResult.Success) return;

        var request = new LotCalculationRequest(
            UserId:                 user.Id,
            AccountId:              SelectedAccount?.Id,
            Symbol:                 SelectedSymbol!,
            Capital:                ParseDecimal(CapitalText)!.Value,
            RiskPercent:            ParseDecimal(RiskPercentText)!.Value,
            EntryPrice:             ParseDecimal(EntryPriceText)!.Value,
            StopLoss:               ParseDecimal(StopLossText)!.Value,
            TakeProfit:             ParseDecimal(TakeProfitText),
            AccountCurrency:        AccountCurrency,
            MaxRiskPercentPerTrade: SelectedAccount?.MaxRiskPercentPerTrade);

        try
        {
            await _calculatorService.SaveAsync(request, _lastResult);
            await LoadHistoryAsync(user.Id);
            GeneralSuccess = "Cálculo guardado en el historial.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving lot calculation");
            ErrorMessage = "Ocurrió un error al guardar el cálculo.";
        }
    }

    private static decimal? ParseDecimal(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        return decimal.TryParse(text, NumberStyles.Any, Ic, out var v) ? v : null;
    }
}
