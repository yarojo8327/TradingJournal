using Application.WPF.Common.ViewModels;
using Application.WPF.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Globalization;
using TradingAccountEntity = Application.WPF.Models.Entities.TradingAccount;

namespace Application.WPF.ViewModels.LotCalculator;

/// <summary>
/// Calculadora de lotaje basada en gestión de riesgo (HU-TRD-001).
/// Calcula el tamaño de posición recomendado a partir del capital, % de riesgo
/// y distancia del Stop Loss, validando las reglas de negocio RN-001, RN-002 y RN-005.
/// </summary>
public partial class LotCalculatorViewModel : BaseViewModel
{
    private readonly ILotCalculatorService     _calculatorService;
    private readonly ISymbolMappingService     _symbolMappingService;
    private readonly ITradingAccountService    _accountService;
    private readonly ISessionService           _sessionService;

    private static readonly CultureInfo Ic = CultureInfo.InvariantCulture;

    public LotCalculatorViewModel(
        ILotCalculatorService            calculatorService,
        ISymbolMappingService            symbolMappingService,
        ITradingAccountService           accountService,
        ISessionService                  sessionService)
    {
        _calculatorService    = calculatorService;
        _symbolMappingService = symbolMappingService;
        _accountService       = accountService;
        _sessionService       = sessionService;
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
    [ObservableProperty] private bool    _hasRewardAmount;
    [ObservableProperty] private string  _lotSizeText      = "—";
    [ObservableProperty] private string  _riskAmountText   = "—";
    [ObservableProperty] private string  _rewardAmountText = "—";
    [ObservableProperty] private string  _riskRewardText   = "—";
    [ObservableProperty] private string  _errorMessage     = string.Empty;
    [ObservableProperty] private string  _warningMessage   = string.Empty;

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

    private async Task RecalculateAsync()
    {
        HasResult       = false;
        ErrorMessage    = string.Empty;
        WarningMessage  = string.Empty;

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

        HasRewardAmount = result.RewardAmount.HasValue;
        RewardAmountText = result.RewardAmount.HasValue
            ? $"{result.RewardAmount:N2} {AccountCurrency}"
            : "—";
    }

    // ── Comandos ──────────────────────────────────────────────────────────

    [RelayCommand]
    private void Clear()
    {
        SelectedSymbol   = null;
        CapitalText      = SelectedAccount?.InitialCapital.ToString("0.##", Ic) ?? string.Empty;
        RiskPercentText  = "1";
        EntryPriceText   = string.Empty;
        StopLossText     = string.Empty;
        TakeProfitText   = string.Empty;
        HasResult        = false;
        HasRewardAmount  = false;
        ErrorMessage     = string.Empty;
        WarningMessage   = string.Empty;
    }

    private static decimal? ParseDecimal(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        return decimal.TryParse(text, NumberStyles.Any, Ic, out var v) ? v : null;
    }
}
