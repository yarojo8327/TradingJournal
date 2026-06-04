using Application.WPF.Common.ViewModels;
using Application.WPF.Services.Interfaces;
using Application.WPF.ViewModels.Login;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Application.WPF.ViewModels.Register;

public partial class RegisterViewModel : BaseViewModel
{
    private readonly IUserService       _userService;
    private readonly ISessionService    _sessionService;
    private readonly INavigationService _navigationService;
    private readonly IDialogService     _dialogService;
    private readonly ILogger<RegisterViewModel> _logger;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "El nombre completo es requerido.")]
    [MinLength(2,   ErrorMessage = "Debe tener al menos 2 caracteres.")]
    [MaxLength(150, ErrorMessage = "No puede superar 150 caracteres.")]
    private string _fullName = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "El correo electrónico es requerido.")]
    [EmailAddress(ErrorMessage = "Formato de correo electrónico inválido.")]
    [MaxLength(200, ErrorMessage = "No puede superar 200 caracteres.")]
    private string _email = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "El nombre de usuario es requerido.")]
    [MinLength(3,  ErrorMessage = "Debe tener al menos 3 caracteres.")]
    [MaxLength(50, ErrorMessage = "No puede superar 50 caracteres.")]
    [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "Solo letras, números y guión bajo.")]
    private string _username = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "La contraseña es requerida.")]
    [CustomValidation(typeof(RegisterViewModel), nameof(ValidatePasswordComplexity))]
    private string _password = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "La confirmación de contraseña es requerida.")]
    [CustomValidation(typeof(RegisterViewModel), nameof(ValidateConfirmPassword))]
    private string _confirmPassword = string.Empty;

    [ObservableProperty]
    private string _generalError = string.Empty;

    public RegisterViewModel(
        IUserService       userService,
        ISessionService    sessionService,
        INavigationService navigationService,
        IDialogService     dialogService,
        ILogger<RegisterViewModel> logger)
    {
        _userService       = userService;
        _sessionService    = sessionService;
        _navigationService = navigationService;
        _dialogService     = dialogService;
        _logger            = logger;
        Title              = "Registro de Usuario";
    }

    partial void OnPasswordChanged(string value) =>
        ValidateProperty(ConfirmPassword, nameof(ConfirmPassword));

    [RelayCommand]
    private async Task RegisterAsync()
    {
        GeneralError = string.Empty;
        ValidateAll();

        if (HasErrors) return;

        IsBusy = true;
        try
        {
            if (await _userService.EmailExistsAsync(Email))
            {
                GeneralError = "El correo electrónico ya está registrado.";
                return;
            }

            if (await _userService.UsernameExistsAsync(Username))
            {
                GeneralError = "El nombre de usuario ya está en uso.";
                return;
            }

            var user = await _userService.RegisterAsync(FullName, Email, Username, Password);
            _logger.LogInformation("Registration successful for {Username}", Username);

            _sessionService.SetUser(user);
            _navigationService.NavigateTo<DashboardViewModel>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during user registration");
            GeneralError = "Ocurrió un error inesperado. Intenta de nuevo.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void GoToLogin() =>
        _navigationService.NavigateTo<LoginViewModel>();

    public static ValidationResult? ValidatePasswordComplexity(string value, ValidationContext _)
    {
        if (string.IsNullOrEmpty(value)) return ValidationResult.Success;
        if (value.Length < 8)                        return new ValidationResult("Mínimo 8 caracteres.");
        if (!Regex.IsMatch(value, @"[A-Z]"))         return new ValidationResult("Debe contener al menos una mayúscula.");
        if (!Regex.IsMatch(value, @"[a-z]"))         return new ValidationResult("Debe contener al menos una minúscula.");
        if (!Regex.IsMatch(value, @"[0-9]"))         return new ValidationResult("Debe contener al menos un número.");
        if (!Regex.IsMatch(value, @"[^a-zA-Z0-9]")) return new ValidationResult("Debe contener al menos un carácter especial.");
        return ValidationResult.Success;
    }

    public static ValidationResult? ValidateConfirmPassword(string value, ValidationContext context)
    {
        var vm = (RegisterViewModel)context.ObjectInstance;
        return value != vm.Password
            ? new ValidationResult("Las contraseñas no coinciden.")
            : ValidationResult.Success;
    }
}
