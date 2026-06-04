using Application.WPF.Common.ViewModels;
using Application.WPF.Services.Interfaces;
using Application.WPF.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;

namespace Application.WPF.ViewModels.Splash;

public partial class SplashViewModel : BaseViewModel
{
    private readonly INavigationService _navigationService;
    private readonly ILocalizationService _localization;
    private readonly ILogger<SplashViewModel> _logger;

    [ObservableProperty]
    private string _loadingStatus = string.Empty;

    [ObservableProperty]
    private double _loadingProgress;

    public SplashViewModel(
        INavigationService navigationService,
        ILocalizationService localization,
        ILogger<SplashViewModel> logger)
    {
        _navigationService = navigationService;
        _localization      = localization;
        _logger            = logger;
        Title              = "Loading";
        _loadingStatus     = localization["Splash_Loading"];
    }

    public override async Task InitializeAsync()
    {
        _logger.LogInformation("Application starting up");

        await UpdateProgress(_localization["Splash_InitConfig"],   20);
        await UpdateProgress(_localization["Splash_InitServices"], 50);
        await UpdateProgress(_localization["Splash_InitWorkspace"],80);
        await UpdateProgress(_localization["Splash_Ready"],        100);

        _logger.LogInformation("Startup complete, navigating to Dashboard");
        _navigationService.NavigateTo<DashboardViewModel>();
    }

    private async Task UpdateProgress(string status, double progress)
    {
        LoadingStatus   = status;
        LoadingProgress = progress;
        await Task.Delay(300);
    }
}
