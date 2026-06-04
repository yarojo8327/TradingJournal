using Application.WPF.Common.Localization;
using Application.WPF.Common.ViewModels;
using Application.WPF.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;

namespace Application.WPF.ViewModels.Main;

public partial class MainViewModel : BaseViewModel
{
    private readonly INavigationService _navigationService;
    private readonly ILocalizationService _localization;
    private readonly ILogger<MainViewModel> _logger;

    [ObservableProperty]
    private BaseViewModel? _currentView;

    [ObservableProperty]
    private SupportedLanguage _selectedLanguage = SupportedLanguage.EnUS;

    public IReadOnlyList<SupportedLanguage> AvailableLanguages => _localization.AvailableLanguages;

    public MainViewModel(
        INavigationService navigationService,
        ILocalizationService localization,
        ILogger<MainViewModel> logger)
    {
        _navigationService = navigationService;
        _localization      = localization;
        _logger            = logger;
        Title              = "Trading Journal";

        _navigationService.Navigated += OnNavigated;
        _currentView = navigationService.CurrentViewModel;

        // Keep selector in sync with current service culture
        _selectedLanguage = SupportedLanguage.All
            .FirstOrDefault(l => l.CultureCode == localization.CurrentCulture)
            ?? SupportedLanguage.EnUS;
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

    public override void Dispose()
    {
        _navigationService.Navigated -= OnNavigated;
        base.Dispose();
    }
}
