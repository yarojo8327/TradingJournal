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

        var rawConnectionString = configuration.GetConnectionString("TradingJournal")
            ?? throw new InvalidOperationException("ConnectionStrings:TradingJournal not found in configuration.");

        var connectionString = rawConnectionString.Replace(
            "%APPDATA%",
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));

        var dbFolder = Path.GetDirectoryName(
            connectionString.Replace("Data Source=", string.Empty).Trim());
        if (!string.IsNullOrEmpty(dbFolder))
            Directory.CreateDirectory(dbFolder);

        services.AddDbContext<TradingJournalDbContext>(
            options => options.UseSqlite(connectionString),
            ServiceLifetime.Transient);

        return services;
    }
}
