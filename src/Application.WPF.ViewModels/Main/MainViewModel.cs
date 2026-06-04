using Application.WPF.Common.Localization;
using Application.WPF.Common.ViewModels;
using Application.WPF.Services.Interfaces;
using Application.WPF.ViewModels.Login;
using Application.WPF.ViewModels.Profile;
using Application.WPF.ViewModels.Register;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace Application.WPF.ViewModels.Main;

public partial class MainViewModel : BaseViewModel
{
    private readonly INavigationService  _navigationService;
    private readonly ILocalizationService _localization;
    private readonly ISessionService     _sessionService;
    private readonly ILogger<MainViewModel> _logger;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowShell))]
    private BaseViewModel? _currentView;

    [ObservableProperty]
    private SupportedLanguage _selectedLanguage = SupportedLanguage.EsCO;

    [ObservableProperty]
    private string _currentUsername = string.Empty;

    [ObservableProperty]
    private bool _isAuthenticated;

    public bool ShowShell => CurrentView is not RegisterViewModel
                                        and not LoginViewModel
                                        and not null;

    public IReadOnlyList<SupportedLanguage> AvailableLanguages => _localization.AvailableLanguages;

    public MainViewModel(
        INavigationService  navigationService,
        ILocalizationService localization,
        ISessionService     sessionService,
        ILogger<MainViewModel> logger)
    {
        _navigationService = navigationService;
        _localization      = localization;
        _sessionService    = sessionService;
        _logger            = logger;
        Title              = "Trading Journal";

        _navigationService.Navigated   += OnNavigated;
        _sessionService.SessionChanged += OnSessionChanged;
        _currentView = navigationService.CurrentViewModel;

        _selectedLanguage = SupportedLanguage.All
            .FirstOrDefault(l => l.CultureCode == localization.CurrentCulture)
            ?? SupportedLanguage.EsCO;
    }

    partial void OnSelectedLanguageChanged(SupportedLanguage value)
    {
        if (value is null) return;
        _localization.ChangeLanguage(value.CultureCode);
        _logger.LogInformation("Language changed to {Culture}", value.CultureCode);
    }

    public override async Task InitializeAsync()
    {
        _logger.LogInformation("MainViewModel initialized");
        await Task.CompletedTask;
    }

    private void OnNavigated(object? sender, NavigationEventArgs e)
    {
        CurrentView = e.ViewModel;
        _logger.LogInformation("View changed to {ViewModel}", e.ViewModel.GetType().Name);
    }

    private void OnSessionChanged(object? sender, Models.Entities.User? user)
    {
        IsAuthenticated = user is not null;
        CurrentUsername = user?.Username ?? string.Empty;
    }

    [RelayCommand]
    private void GoToDashboard() =>
        _navigationService.NavigateTo<DashboardViewModel>();

    [RelayCommand]
    private void GoToSettings() =>
        _navigationService.NavigateTo<ProfileViewModel>();

    [RelayCommand]
    private void Logout()
    {
        _sessionService.Clear();
        _logger.LogInformation("User logged out");
        _navigationService.NavigateTo<LoginViewModel>();
    }

    public override void Dispose()
    {
        _navigationService.Navigated   -= OnNavigated;
        _sessionService.SessionChanged -= OnSessionChanged;
        base.Dispose();
    }
}
