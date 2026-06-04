using Application.WPF.Common.ViewModels;
using Application.WPF.Models.Enums;
using Application.WPF.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;

namespace Application.WPF.ViewModels.TradingAccount;

public partial class TradingAccountViewModel : BaseViewModel
{
    private readonly ITradingAccountService _accountService;
    private readonly ISessionService        _sessionService;
    private readonly ILogger<TradingAccountViewModel> _logger;

    private int _accountId;

    // ── Campos del formulario ─────────────────────────────────────────────

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "El broker es requerido.")]
    [MaxLength(100, ErrorMessage = "No puede superar 100 caracteres.")]
    private string _broker = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "El número de cuenta es requerido.")]
    [MaxLength(50, ErrorMessage = "No puede superar 50 caracteres.")]
    private string _accountNumber = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "El tipo de cuenta es requerido.")]
    private AccountTypeOption? _selectedAccountType;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "El capital inicial es requerido.")]
    [CustomValidation(typeof(TradingAccountViewModel), nameof(ValidateCapital))]
    private string _initialCapitalText = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "La divisa base es requerida.")]
    [MaxLength(10, ErrorMessage = "No puede superar 10 caracteres.")]
    [RegularExpression(@"^[A-Za-z]{2,10}$", ErrorMessage = "Ingresa un código de divisa válido (ej: USD).")]
    private string _baseCurrency = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "El apalancamiento es requerido.")]
    [RegularExpression(@"^1:[0-9]{1,4}$", ErrorMessage = "Formato requerido: 1:100")]
    private string _leverage = string.Empty;

    [ObservableProperty]
    private DateTime _startDate = DateTime.Today;

    // ── Estado ────────────────────────────────────────────────────────────

    [ObservableProperty] private bool   _isEditMode;
    [ObservableProperty] private string _generalError   = string.Empty;
    [ObservableProperty] private string _generalSuccess = string.Empty;

    // ── Opciones de tipo de cuenta ────────────────────────────────────────

    public IReadOnlyList<AccountTypeOption> AccountTypes { get; } = new List<AccountTypeOption>
    {
        new(AccountType.Demo, "Demo"),
        new(AccountType.Real, "Real / Live"),
        new(AccountType.Prop, "Prop Trading")
    };

    public TradingAccountViewModel(
        ITradingAccountService accountService,
        ISessionService        sessionService,
        ILogger<TradingAccountViewModel> logger)
    {
        _accountService = accountService;
        _sessionService = sessionService;
        _logger         = logger;
        Title           = "Cuenta de Trading";
    }

    public override async Task InitializeAsync()
    {
        var user = _sessionService.CurrentUser;
        if (user is null) return;

        var account = await _accountService.GetByUserIdAsync(user.Id);
        if (account is null)
        {
            IsEditMode = false;
            return;
        }

        IsEditMode          = true;
        _accountId          = account.Id;
        Broker              = account.Broker;
        AccountNumber       = account.AccountNumber;
        SelectedAccountType = AccountTypes.FirstOrDefault(o => o.Value == account.AccountType);
        InitialCapitalText  = account.InitialCapital.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        BaseCurrency        = account.BaseCurrency;
        Leverage            = account.Leverage;
        StartDate           = account.StartDate;
    }

    // ── Guardar ───────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task SaveAsync()
    {
        GeneralError   = string.Empty;
        GeneralSuccess = string.Empty;
        ValidateFormFields();

        if (HasErrors) return;

        if (!decimal.TryParse(InitialCapitalText,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var capital))
        {
            GeneralError = "El capital inicial no tiene un formato válido.";
            return;
        }

        var user = _sessionService.CurrentUser;
        if (user is null) return;

        IsBusy = true;
        try
        {
            if (IsEditMode)
            {
                await _accountService.UpdateAsync(
                    _accountId, Broker, AccountNumber,
                    SelectedAccountType!.Value, capital,
                    BaseCurrency, Leverage, StartDate);
                GeneralSuccess = "Cuenta de trading actualizada correctamente.";
            }
            else
            {
                var account = await _accountService.CreateAsync(
                    user.Id, Broker, AccountNumber,
                    SelectedAccountType!.Value, capital,
                    BaseCurrency, Leverage, StartDate);
                _accountId = account.Id;
                IsEditMode = true;
                GeneralSuccess = "Cuenta de trading registrada correctamente.";
            }

            _logger.LogInformation("Trading account saved for user {UserId}", user.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving trading account");
            GeneralError = "Ocurrió un error al guardar la cuenta de trading.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ── Validaciones ──────────────────────────────────────────────────────

    public static ValidationResult? ValidateCapital(string value, ValidationContext _)
    {
        if (string.IsNullOrWhiteSpace(value)) return ValidationResult.Success;
        if (!decimal.TryParse(value,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var d) || d <= 0)
            return new ValidationResult("Debe ser un número mayor que cero.");
        return ValidationResult.Success;
    }

    private void ValidateFormFields()
    {
        ValidateProperty(Broker,             nameof(Broker));
        ValidateProperty(AccountNumber,      nameof(AccountNumber));
        ValidateProperty(SelectedAccountType, nameof(SelectedAccountType));
        ValidateProperty(InitialCapitalText, nameof(InitialCapitalText));
        ValidateProperty(BaseCurrency,       nameof(BaseCurrency));
        ValidateProperty(Leverage,           nameof(Leverage));
    }
}
