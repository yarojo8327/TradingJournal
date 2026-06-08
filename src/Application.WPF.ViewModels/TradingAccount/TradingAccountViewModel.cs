using Application.WPF.Common.ViewModels;
using Application.WPF.Models.Enums;
using TradingAccountEntity = Application.WPF.Models.Entities.TradingAccount;
using Application.WPF.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;

namespace Application.WPF.ViewModels.TradingAccount;

public partial class TradingAccountViewModel : BaseViewModel
{
    private readonly ITradingAccountService          _accountService;
    private readonly ISessionService                 _sessionService;
    private readonly IDialogService                  _dialogService;
    private readonly ILogger<TradingAccountViewModel> _logger;

    private int _accountId;

    // ── Lista de cuentas ──────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoAccounts))]
    private ObservableCollection<TradingAccountEntity> _accounts = new();

    [ObservableProperty] private bool _isFormVisible;
    [ObservableProperty] private string _formTitle = "Nueva cuenta de trading";

    public bool HasNoAccounts => !Accounts.Any();

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
    private string _baseCurrency = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "El apalancamiento es requerido.")]
    [RegularExpression(@"^1:[0-9]{1,4}$", ErrorMessage = "Formato requerido: 1:100  (ej: 1:50, 1:200, 1:500)")]
    private string _leverage = string.Empty;

    [ObservableProperty] private DateTime _startDate = DateTime.Today;

    // ── Estado ────────────────────────────────────────────────────────────

    [ObservableProperty] private bool   _isEditMode;
    [ObservableProperty] private string _generalError   = string.Empty;
    [ObservableProperty] private string _generalSuccess = string.Empty;

    // ── Catálogos ─────────────────────────────────────────────────────────

    public IReadOnlyList<AccountTypeOption> AccountTypes { get; } = new List<AccountTypeOption>
    {
        new(AccountType.Demo, "Demo"),
        new(AccountType.Real, "Real / Live"),
        new(AccountType.Prop, "Prop Trading")
    };

    public IReadOnlyList<string> Currencies { get; } = new List<string>
    {
        "USD", "EUR", "GBP", "JPY", "CHF", "CAD", "AUD", "NZD",
        "COP", "MXN", "BRL", "CLP", "ARS",
        "HKD", "SGD", "NOK", "SEK", "DKK", "ZAR", "TRY", "CNH"
    };

    public IReadOnlyList<string> LeverageOptions { get; } = new List<string>
    {
        "1:1", "1:2", "1:5", "1:10", "1:20", "1:30",
        "1:50", "1:100", "1:200", "1:300", "1:500"
    };

    public TradingAccountViewModel(
        ITradingAccountService           accountService,
        ISessionService                  sessionService,
        IDialogService                   dialogService,
        ILogger<TradingAccountViewModel> logger)
    {
        _accountService = accountService;
        _sessionService = sessionService;
        _dialogService  = dialogService;
        _logger         = logger;
        Title           = "Cuentas de Trading";
    }

    public override async Task InitializeAsync()
    {
        var user = _sessionService.CurrentUser;
        if (user is null) return;

        await LoadAccountsAsync(user.Id);

        if (!Accounts.Any())
        {
            IsFormVisible = true;
            FormTitle     = "Registrar cuenta de trading";
        }
    }

    private async Task LoadAccountsAsync(int userId)
    {
        var list = await _accountService.GetAllByUserIdAsync(userId);
        Accounts = new ObservableCollection<TradingAccountEntity>(list);
    }

    // ── Comandos de lista ─────────────────────────────────────────────────

    [RelayCommand]
    private void NewAccount()
    {
        ClearForm();
        IsEditMode    = false;
        IsFormVisible = true;
        FormTitle     = "Nueva cuenta de trading";
        GeneralError  = string.Empty;
        GeneralSuccess = string.Empty;
    }

    [RelayCommand]
    private void EditAccount(TradingAccountEntity account)
    {
        _accountId          = account.Id;
        Broker              = account.Broker;
        AccountNumber       = account.AccountNumber;
        SelectedAccountType = AccountTypes.FirstOrDefault(o => o.Value == account.AccountType);
        InitialCapitalText  = account.InitialCapital.ToString("F2",
                                  System.Globalization.CultureInfo.InvariantCulture);
        BaseCurrency        = account.BaseCurrency;
        Leverage            = account.Leverage;
        StartDate           = account.StartDate;
        IsEditMode          = true;
        IsFormVisible       = true;
        FormTitle           = "Editar cuenta de trading";
        GeneralError        = string.Empty;
        GeneralSuccess      = string.Empty;
    }

    [RelayCommand]
    private async Task DeleteAccountAsync(TradingAccountEntity account)
    {
        // ── Verificar que no tenga trades registrados ──────────────────────
        var hasTrades = await _accountService.HasTradesAsync(account.Id);
        if (hasTrades)
        {
            GeneralError   = $"No se puede eliminar '{account.Broker} — {account.AccountNumber}': " +
                              "tiene operaciones registradas en la bitácora. " +
                              "Elimina primero los trades asociados.";
            GeneralSuccess = string.Empty;
            return;
        }

        // ── Confirmación ───────────────────────────────────────────────────
        var confirmed = _dialogService.ShowConfirmation(
            $"¿Eliminar la cuenta '{account.Broker} — {account.AccountNumber}'?\nEsta acción no se puede deshacer.",
            "Confirmar eliminación");

        if (!confirmed) return;

        IsBusy = true;
        try
        {
            await _accountService.DeleteAsync(account.Id);

            var user = _sessionService.CurrentUser;
            if (user is not null) await LoadAccountsAsync(user.Id);

            GeneralSuccess = $"Cuenta '{account.Broker} — {account.AccountNumber}' eliminada correctamente.";
            GeneralError   = string.Empty;

            // Si ya no hay cuentas, mostrar el formulario de alta
            if (!Accounts.Any())
            {
                IsFormVisible = true;
                FormTitle     = "Registrar cuenta de trading";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting account {Id}", account.Id);
            GeneralError   = "Error al eliminar la cuenta.";
            GeneralSuccess = string.Empty;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        ClearForm();
        IsFormVisible  = false;
        GeneralError   = string.Empty;
        GeneralSuccess = string.Empty;
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
                GeneralSuccess = "Cuenta actualizada correctamente.";
            }
            else
            {
                await _accountService.CreateAsync(
                    user.Id, Broker, AccountNumber,
                    SelectedAccountType!.Value, capital,
                    BaseCurrency, Leverage, StartDate);
                GeneralSuccess = "Cuenta registrada correctamente.";
            }

            await LoadAccountsAsync(user.Id);
            IsFormVisible = false;
            ClearForm();
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

    private void ClearForm()
    {
        _accountId          = 0;
        Broker              = string.Empty;
        AccountNumber       = string.Empty;
        SelectedAccountType = null;
        InitialCapitalText  = string.Empty;
        BaseCurrency        = string.Empty;
        Leverage            = string.Empty;
        StartDate           = DateTime.Today;
        ClearErrors();
    }
}
