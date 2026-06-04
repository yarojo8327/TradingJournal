using Application.WPF.Common.ViewModels;
using Application.WPF.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Application.WPF.ViewModels.Profile;

public partial class ProfileViewModel : BaseViewModel
{
    private readonly IUserService    _userService;
    private readonly ISessionService _sessionService;
    private readonly IDialogService  _dialogService;
    private readonly ILogger<ProfileViewModel> _logger;

    // ── Datos del perfil ──────────────────────────────────────────────────

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
    private string _memberSince = string.Empty;

    // ── Cambio de contraseña ──────────────────────────────────────────────

    [ObservableProperty]
    private string _currentPassword = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [CustomValidation(typeof(ProfileViewModel), nameof(ValidateNewPassword))]
    private string _newPassword = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [CustomValidation(typeof(ProfileViewModel), nameof(ValidateConfirmNewPassword))]
    private string _confirmNewPassword = string.Empty;

    // ── Estado ────────────────────────────────────────────────────────────

    [ObservableProperty] private string _profileError   = string.Empty;
    [ObservableProperty] private string _profileSuccess = string.Empty;
    [ObservableProperty] private string _passwordError   = string.Empty;
    [ObservableProperty] private string _passwordSuccess = string.Empty;

    public ProfileViewModel(
        IUserService    userService,
        ISessionService sessionService,
        IDialogService  dialogService,
        ILogger<ProfileViewModel> logger)
    {
        _userService    = userService;
        _sessionService = sessionService;
        _dialogService  = dialogService;
        _logger         = logger;
        Title           = "Mi perfil";
    }

    public override Task InitializeAsync()
    {
        var user = _sessionService.CurrentUser;
        if (user is null) return Task.CompletedTask;

        FullName    = user.FullName;
        Email       = user.Email;
        Username    = user.Username;
        MemberSince = user.CreatedAt.ToLocalTime().ToString("dd 'de' MMMM 'de' yyyy",
                          new System.Globalization.CultureInfo("es-CO"));
        return Task.CompletedTask;
    }

    partial void OnNewPasswordChanged(string value) =>
        ValidateProperty(ConfirmNewPassword, nameof(ConfirmNewPassword));

    // ── Guardar perfil ────────────────────────────────────────────────────

    [RelayCommand]
    private async Task SaveProfileAsync()
    {
        ProfileError   = string.Empty;
        ProfileSuccess = string.Empty;
        ValidateAll();

        if (HasErrors) return;

        var user = _sessionService.CurrentUser;
        if (user is null) return;

        IsBusy = true;
        try
        {
            var emailOwner = await _userService.EmailExistsAsync(Email);
            if (emailOwner && Email != user.Email)
            {
                ProfileError = "El correo electrónico ya está en uso por otra cuenta.";
                return;
            }

            var usernameOwner = await _userService.UsernameExistsAsync(Username);
            if (usernameOwner && Username != user.Username)
            {
                ProfileError = "El nombre de usuario ya está en uso por otra cuenta.";
                return;
            }

            var updated = await _userService.UpdateProfileAsync(user.Id, FullName, Email, Username);
            _sessionService.SetUser(updated);
            ProfileSuccess = "Perfil actualizado correctamente.";
            _logger.LogInformation("Profile updated for {Username}", updated.Username);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating profile");
            ProfileError = "Ocurrió un error al guardar el perfil.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ── Cambiar contraseña ────────────────────────────────────────────────

    [RelayCommand]
    private async Task ChangePasswordAsync()
    {
        PasswordError   = string.Empty;
        PasswordSuccess = string.Empty;

        if (string.IsNullOrWhiteSpace(CurrentPassword))
        {
            PasswordError = "Ingresa tu contraseña actual.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewPassword))
        {
            PasswordError = "Ingresa la nueva contraseña.";
            return;
        }

        ValidateProperty(NewPassword,       nameof(NewPassword));
        ValidateProperty(ConfirmNewPassword, nameof(ConfirmNewPassword));
        if (GetErrors(nameof(NewPassword)).Cast<object>().Any() ||
            GetErrors(nameof(ConfirmNewPassword)).Cast<object>().Any())
            return;

        var user = _sessionService.CurrentUser;
        if (user is null) return;

        IsBusy = true;
        try
        {
            var authenticated = await _userService.AuthenticateAsync(user.Username, CurrentPassword);
            if (authenticated is null)
            {
                PasswordError = "La contraseña actual es incorrecta.";
                return;
            }

            await _userService.ChangePasswordAsync(user.Id, NewPassword);
            CurrentPassword    = string.Empty;
            NewPassword        = string.Empty;
            ConfirmNewPassword = string.Empty;
            PasswordSuccess    = "Contraseña actualizada correctamente.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing password");
            PasswordError = "Ocurrió un error al cambiar la contraseña.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ── Validaciones de contraseña ────────────────────────────────────────

    public static ValidationResult? ValidateNewPassword(string value, ValidationContext _)
    {
        if (string.IsNullOrEmpty(value)) return ValidationResult.Success;
        if (value.Length < 8)                        return new ValidationResult("Mínimo 8 caracteres.");
        if (!Regex.IsMatch(value, @"[A-Z]"))         return new ValidationResult("Debe contener al menos una mayúscula.");
        if (!Regex.IsMatch(value, @"[a-z]"))         return new ValidationResult("Debe contener al menos una minúscula.");
        if (!Regex.IsMatch(value, @"[0-9]"))         return new ValidationResult("Debe contener al menos un número.");
        if (!Regex.IsMatch(value, @"[^a-zA-Z0-9]")) return new ValidationResult("Debe contener al menos un carácter especial.");
        return ValidationResult.Success;
    }

    public static ValidationResult? ValidateConfirmNewPassword(string value, ValidationContext context)
    {
        var vm = (ProfileViewModel)context.ObjectInstance;
        return value != vm.NewPassword
            ? new ValidationResult("Las contraseñas no coinciden.")
            : ValidationResult.Success;
    }
}
