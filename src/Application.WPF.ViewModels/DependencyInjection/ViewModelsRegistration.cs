using Application.WPF.ViewModels.Login;
using Application.WPF.ViewModels.Main;
using Application.WPF.ViewModels.Register;
using Application.WPF.ViewModels.Splash;
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
        return services;
    }
}
