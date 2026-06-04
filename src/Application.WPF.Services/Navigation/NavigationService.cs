using Application.WPF.Common.ViewModels;
using Application.WPF.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Application.WPF.Services.Navigation;

public class NavigationService : INavigationService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NavigationService> _logger;
    private readonly Stack<BaseViewModel> _backStack = new();

    public BaseViewModel? CurrentViewModel { get; private set; }
    public bool CanNavigateBack => _backStack.Count > 0;
    public event EventHandler<NavigationEventArgs>? Navigated;

    public NavigationService(IServiceProvider serviceProvider, ILogger<NavigationService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public void NavigateTo<TViewModel>(object? parameter = null) where TViewModel : BaseViewModel
    {
        _logger.LogInformation("Navigating to {ViewModel}", typeof(TViewModel).Name);

        if (CurrentViewModel is not null)
            _backStack.Push(CurrentViewModel);

        var viewModel = _serviceProvider.GetRequiredService<TViewModel>();
        CurrentViewModel = viewModel;

        Navigated?.Invoke(this, new NavigationEventArgs(viewModel));

        AsyncHelper.FireAndForget(viewModel.InitializeAsync());
    }

    public void NavigateBack()
    {
        if (!CanNavigateBack) return;

        _logger.LogInformation("Navigating back");
        CurrentViewModel = _backStack.Pop();
        Navigated?.Invoke(this, new NavigationEventArgs(CurrentViewModel));
    }

    private static class AsyncHelper
    {
        public static void FireAndForget(Task task) =>
            task.ContinueWith(t =>
            {
                if (t.IsFaulted) throw t.Exception!.InnerException ?? t.Exception;
            }, TaskScheduler.Default);
    }
}
