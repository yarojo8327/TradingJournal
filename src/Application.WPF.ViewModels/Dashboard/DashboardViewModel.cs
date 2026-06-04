using Application.WPF.Common.ViewModels;
using Application.WPF.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace Application.WPF.ViewModels;

public partial class DashboardViewModel : BaseViewModel
{
    private readonly IDialogService _dialogService;
    private readonly ILogger<DashboardViewModel> _logger;

    [ObservableProperty]
    private string _welcomeMessage = "Welcome to TradingJournal";

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public DashboardViewModel(IDialogService dialogService, ILogger<DashboardViewModel> logger)
    {
        _dialogService = dialogService;
        _logger = logger;
        Title = "Dashboard";
    }

    public override async Task InitializeAsync()
    {
        _logger.LogInformation("Dashboard initialized");
        StatusMessage = $"Ready — {DateTime.Now:yyyy-MM-dd HH:mm}";
        await Task.CompletedTask;
    }

    [RelayCommand]
    private void ShowSampleDialog() =>
        _dialogService.ShowInformation("Baseline architecture is ready.", "TradingJournal");

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsBusy = true;
        _logger.LogInformation("Dashboard refresh requested");
        await Task.Delay(500);
        StatusMessage = $"Refreshed — {DateTime.Now:yyyy-MM-dd HH:mm}";
        IsBusy = false;
    }
}
