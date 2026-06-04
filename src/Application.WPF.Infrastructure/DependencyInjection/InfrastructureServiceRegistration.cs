using Application.WPF.Infrastructure.Data;
using Application.WPF.Models.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Application.WPF.Infrastructure.DependencyInjection;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AppSettings>(configuration.GetSection(AppSettings.SectionName));

        var dbFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TradingJournal");
        Directory.CreateDirectory(dbFolder);
        var dbPath = Path.Combine(dbFolder, "tradingjournal.db");

        services.AddDbContext<TradingJournalDbContext>(
            options => options.UseSqlite($"Data Source={dbPath}"),
            ServiceLifetime.Transient);

        return services;
    }
}
