using Application.WPF.Common.ViewModels;

namespace Application.WPF.Services.Interfaces;

public interface INavigationService
{
    BaseViewModel? CurrentViewModel { get; }
    void NavigateTo<TViewModel>(object? parameter = null) where TViewModel : BaseViewModel;
    void NavigateBack();
    bool CanNavigateBack { get; }
    event EventHandler<NavigationEventArgs>? Navigated;
}

public class NavigationEventArgs : EventArgs
{
    public BaseViewModel ViewModel { get; }
    public NavigationEventArgs(BaseViewModel viewModel) => ViewModel = viewModel;
}
