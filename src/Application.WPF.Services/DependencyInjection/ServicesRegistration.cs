using Application.WPF.Services.Configuration;
using Application.WPF.Services.Dialog;
using Application.WPF.Services.Interfaces;
using Application.WPF.Services.Localization;
using Application.WPF.Services.Navigation;
using Application.WPF.Services.Session;
using Application.WPF.Services.Users;
using Microsoft.Extensions.DependencyInjection;

namespace Application.WPF.Services.DependencyInjection;

public static class ServicesRegistration
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IConfigurationService, ConfigurationService>();
        services.AddSingleton<ILocalizationService>(sp =>
            new LocalizationService(
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<LocalizationService>>(),
                "en-US"));
        services.AddTransient<IUserService, UserService>();
        services.AddSingleton<ISessionService, SessionService>();
        return services;
    }
}
