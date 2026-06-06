using Application.WPF.Common.ViewModels;
using Application.WPF.Services.Interfaces;
using Application.WPF.ViewModels;
using Application.WPF.ViewModels.Login;
using Application.WPF.ViewModels.Register;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;

namespace Application.WPF.ViewModels.Splash;

public partial class SplashViewModel : BaseViewModel
{
    private readonly INavigationService          _navigationService;
    private readonly ILocalizationService        _localization;
    private readonly IUserService                _userService;
    private readonly ISessionService             _sessionService;
    private readonly ISessionPersistenceService  _sessionPersistence;
    private readonly ILogger<SplashViewModel>    _logger;

    [ObservableProperty] private string _loadingStatus  = string.Empty;
    [ObservableProperty] private double _loadingProgress;

    public SplashViewModel(
        INavigationService         navigationService,
        ILocalizationService       localization,
        IUserService               userService,
        ISessionService            sessionService,
        ISessionPersistenceService sessionPersistence,
        ILogger<SplashViewModel>   logger)
    {
        _navigationService  = navigationService;
        _localization       = localization;
        _userService        = userService;
        _sessionService     = sessionService;
        _sessionPersistence = sessionPersistence;
        _logger             = logger;
        Title               = "Loading";
        _loadingStatus      = localization["Splash_Loading"];
    }

    public override async Task InitializeAsync()
    {
        _logger.LogInformation("Application starting up");

        await UpdateProgress(_localization["Splash_InitConfig"],   20);
        await UpdateProgress(_localization["Splash_InitServices"], 50);
        await UpdateProgress(_localization["Splash_InitWorkspace"],80);
        await UpdateProgress(_localization["Splash_Ready"],        100);

        _logger.LogInformation("Startup complete, checking session");

        // Intentar restaurar sesión persistida
        var savedUserId = await _sessionPersistence.TryGetSavedUserIdAsync();
        if (savedUserId.HasValue)
        {
            var user = await _userService.GetByIdAsync(savedUserId.Value);
            if (user is not null)
            {
                _logger.LogInformation("Restoring session for user {Username}", user.Username);
                _sessionService.SetUser(user);
                _navigationService.NavigateTo<DashboardViewModel>();
                return;
            }

            // Usuario no existe en DB (cuenta eliminada) → limpiar sesión huérfana
            await _sessionPersistence.ClearAsync();
        }

        if (await _userService.AnyUserExistsAsync())
            _navigationService.NavigateTo<LoginViewModel>();
        else
            _navigationService.NavigateTo<RegisterViewModel>();
    }

    private async Task UpdateProgress(string status, double progress)
    {
        LoadingStatus   = status;
        LoadingProgress = progress;
        await Task.Delay(300);
    }
}
