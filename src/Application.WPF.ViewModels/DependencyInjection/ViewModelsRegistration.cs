using Application.WPF.ViewModels.Main;
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
        return services;
    }
}
