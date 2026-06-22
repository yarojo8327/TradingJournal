using Application.WPF.ViewModels.Journal;
using Application.WPF.ViewModels.Login;
using Application.WPF.ViewModels.LotCalculator;
using Application.WPF.ViewModels.Main;
using Application.WPF.ViewModels.Playbook;
using Application.WPF.ViewModels.Profile;
using Application.WPF.ViewModels.Register;
using Application.WPF.ViewModels.Settings;
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
        services.AddTransient<TradeJournalViewModel>();
        services.AddTransient<TradeAnalyticsViewModel>();
        services.AddTransient<PlaybookViewModel>();
        services.AddTransient<JournalSettingsViewModel>();
        services.AddTransient<EmotionalStatesViewModel>();
        services.AddTransient<MistakeTypesViewModel>();
        services.AddTransient<SymbolMappingsViewModel>();
        services.AddTransient<LotCalculatorViewModel>();
        services.AddTransient<StrategyRaterViewModel>();
        services.AddTransient<Func<StrategyRaterViewModel>>(sp => () => sp.GetRequiredService<StrategyRaterViewModel>());
        return services;
    }
}
