using Application.WPF.Services.Configuration;
using Application.WPF.Services.Dialog;
using Application.WPF.Services.Interfaces;
using Application.WPF.Services.Localization;
using Application.WPF.Services.Navigation;
using Application.WPF.Services.Journal;
using Application.WPF.Services.LotCalculator;
using Application.WPF.Services.Symbols;
using Application.WPF.Services.Playbook;
using Application.WPF.Services.Session;
using Application.WPF.Services.Strategies;
using Application.WPF.Services.Trades;
using Application.WPF.Services.TradingAccounts;
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
                "es-CO"));
        services.AddTransient<IUserService, UserService>();
        services.AddTransient<ITradingAccountService, TradingAccountService>();
        services.AddTransient<ITradingStrategyService, TradingStrategyService>();
        services.AddTransient<ITradeService, TradeService>();
        services.AddTransient<IJournalListService, JournalListService>();
        services.AddTransient<ISymbolMappingService, SymbolMappingService>();
        services.AddTransient<ILotCalculatorService, LotCalculatorService>();
        services.AddTransient<IPlaybookService, PlaybookService>();
        services.AddSingleton<ISessionPersistenceService, SessionPersistenceService>();
        services.AddSingleton<ISessionService, SessionService>();
        return services;
    }
}
