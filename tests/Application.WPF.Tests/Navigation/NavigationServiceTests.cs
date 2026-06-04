using Application.WPF.Common.ViewModels;
using Application.WPF.Services.Navigation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Application.WPF.Tests.Navigation;

public class NavigationServiceTests
{
    private readonly NavigationService _sut;

    public NavigationServiceTests()
    {
        var services = new ServiceCollection();
        services.AddTransient<TestViewModel>();
        services.AddTransient<AnotherViewModel>();
        var provider = services.BuildServiceProvider();

        _sut = new NavigationService(provider, NullLogger<NavigationService>.Instance);
    }

    [Fact]
    public void NavigateTo_SetsCurrentViewModel()
    {
        _sut.NavigateTo<TestViewModel>();

        Assert.NotNull(_sut.CurrentViewModel);
        Assert.IsType<TestViewModel>(_sut.CurrentViewModel);
    }

    [Fact]
    public void NavigateTo_RaisesNavigatedEvent()
    {
        BaseViewModel? received = null;
        _sut.Navigated += (_, e) => received = e.ViewModel;

        _sut.NavigateTo<TestViewModel>();

        Assert.NotNull(received);
        Assert.IsType<TestViewModel>(received);
    }

    [Fact]
    public void CanNavigateBack_FalseWhenNoHistory()
    {
        Assert.False(_sut.CanNavigateBack);
    }

    [Fact]
    public void CanNavigateBack_TrueAfterSecondNavigation()
    {
        _sut.NavigateTo<TestViewModel>();
        _sut.NavigateTo<AnotherViewModel>();

        Assert.True(_sut.CanNavigateBack);
    }

    [Fact]
    public void NavigateBack_RestoresPreviousViewModel()
    {
        _sut.NavigateTo<TestViewModel>();
        _sut.NavigateTo<AnotherViewModel>();

        _sut.NavigateBack();

        Assert.IsType<TestViewModel>(_sut.CurrentViewModel);
    }

    [Fact]
    public void NavigateBack_WhenNoHistory_DoesNothing()
    {
        _sut.NavigateTo<TestViewModel>();
        _sut.NavigateBack();

        Assert.IsType<TestViewModel>(_sut.CurrentViewModel);
    }
}

public class TestViewModel : BaseViewModel { }
public class AnotherViewModel : BaseViewModel { }
