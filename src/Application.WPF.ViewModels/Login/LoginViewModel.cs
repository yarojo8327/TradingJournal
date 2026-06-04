using Application.WPF.Common.ViewModels;
using Application.WPF.Services.Interfaces;
using Application.WPF.ViewModels;
using Application.WPF.ViewModels.Register;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;

namespace Application.WPF.ViewModels.Login;

public partial class LoginViewModel : BaseViewModel
{
    private readonly IUserService       _userService;
    private readonly ISessionService    _sessionService;
    private readonly INavigationService _navigationService;
    private readonly ILogger<LoginViewModel> _logger;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "El usuario o correo es requerido.")]
    private string _usernameOrEmail = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _generalError = string.Empty;

    public LoginViewModel(
        IUserService       userService,
        ISessionService    sessionService,
        INavigationService navigationService,
        ILogger<LoginViewModel> logger)
    {
        _userService       = userService;
        _sessionService    = sessionService;
        _navigationService = navigationService;
        _logger            = logger;
        Title              = "Iniciar sesión";
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        GeneralError = string.Empty;
        ValidateAll();

        if (HasErrors || string.IsNullOrWhiteSpace(Password))
        {
            if (string.IsNullOrWhiteSpace(Password))
                GeneralError = "La contraseña es requerida.";
            return;
        }

        IsBusy = true;
        try
        {
            var user = await _userService.AuthenticateAsync(UsernameOrEmail, Password);
            if (user is null)
            {
                GeneralError = "Usuario o contraseña incorrectos.";
                return;
            }

            _sessionService.SetUser(user);
            _logger.LogInformation("Login successful: {Username}", user.Username);
            _navigationService.NavigateTo<DashboardViewModel>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login");
            GeneralError = "Ocurrió un error inesperado. Intenta de nuevo.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void GoToRegister() =>
        _navigationService.NavigateTo<RegisterViewModel>();
}
