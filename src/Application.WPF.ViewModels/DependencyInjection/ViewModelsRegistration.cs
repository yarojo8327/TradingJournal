using Application.WPF.ViewModels.Login;
using Application.WPF.ViewModels.Main;
using Application.WPF.ViewModels.Profile;
using Application.WPF.ViewModels.Register;
using Application.WPF.ViewModels.Splash;
using Application.WPF.ViewModels.Strategies;
using Application.WPF.ViewModels.TradingAccount;
using Microsoft.Extensions.DependencyInjection;

namespace Application.WPF.ViewModels.DependencyInjection;

public static class ViewModelsRegistration
{
    public static IServiceCollection AddViewModels(this IServiceCollection services)
    {
        services.AddTransient<MainViewModel>();
        services.AddTransient<SplashViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<RegisterViewModel>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<ProfileViewModel>();
        services.AddTransient<TradingAccountViewModel>();
        services.AddTransient<TradingStrategyViewModel>();
        return services;
    }
}
