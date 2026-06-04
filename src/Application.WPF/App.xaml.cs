using Application.WPF.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Application.WPF.Infrastructure.DependencyInjection;
using Application.WPF.Infrastructure.Logging;
using Application.WPF.Models.Configuration;
using Application.WPF.Services.DependencyInjection;
using Application.WPF.Services.Interfaces;
using Application.WPF.ViewModels.DependencyInjection;
using Application.WPF.ViewModels.Splash;
using Application.WPF.Views.Main;
using Application.WPF.Views.Splash;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace Application.WPF;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        _host = BuildHost();
        await _host.StartAsync();

        // Initialize database schema
        using (var db = _host.Services.GetRequiredService<TradingJournalDbContext>())
        {
            await db.Database.EnsureCreatedAsync();
            await EnsureSchemaUpToDateAsync(db);
        }

        // Expose LocalizationService as "Loc" in Application.Resources for {loc:Tr} bindings
        var locService = _host.Services.GetRequiredService<ILocalizationService>();
        Resources["Loc"] = locService;

        // Resolve MainWindow/MainViewModel BEFORE the splash runs so that MainViewModel
        // subscribes to INavigationService.Navigated before InitializeAsync fires it.
        var mainWindow  = _host.Services.GetRequiredService<MainWindow>();
        var mainViewModel = _host.Services.GetRequiredService<ViewModels.Main.MainViewModel>();
        await mainViewModel.InitializeAsync();

        var splashViewModel = _host.Services.GetRequiredService<SplashViewModel>();
        var splash = new SplashView(splashViewModel);
        splash.Show();

        await splashViewModel.InitializeAsync();

        mainWindow.Show();
        splash.Close();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    private static async Task EnsureSchemaUpToDateAsync(TradingJournalDbContext db)
    {
        // EnsureCreated only creates the schema if the DB is new.
        // This method creates any tables that were added after the initial DB creation,
        // preserving existing data. Each statement is idempotent.
        await db.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS ""TradingAccounts"" (
                ""Id""             INTEGER NOT NULL CONSTRAINT ""PK_TradingAccounts"" PRIMARY KEY AUTOINCREMENT,
                ""UserId""         INTEGER NOT NULL,
                ""Broker""         TEXT    NOT NULL,
                ""AccountNumber""  TEXT    NOT NULL,
                ""AccountType""    TEXT    NOT NULL,
                ""InitialCapital"" TEXT    NOT NULL,
                ""BaseCurrency""   TEXT    NOT NULL,
                ""Leverage""       TEXT    NOT NULL,
                ""StartDate""      TEXT    NOT NULL,
                ""CreatedAt""      TEXT    NOT NULL,
                ""UpdatedAt""      TEXT,
                CONSTRAINT ""FK_TradingAccounts_Users_UserId""
                    FOREIGN KEY (""UserId"") REFERENCES ""Users"" (""Id"") ON DELETE CASCADE
            );");

        await db.Database.ExecuteSqlRawAsync(@"
            CREATE UNIQUE INDEX IF NOT EXISTS ""IX_TradingAccounts_UserId""
            ON ""TradingAccounts"" (""UserId"");");
    }

    private static IHost BuildHost()
    {
        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        var appSettings = configuration.GetSection(AppSettings.SectionName).Get<AppSettings>() ?? new();

        Log.Logger = SerilogConfiguration
            .CreateDefault(appSettings.Logging.MinimumLevel)
            .CreateLogger();

        Log.Information("Starting {App} v{Version} [{Env}]",
            appSettings.ApplicationName, appSettings.Version, environment);

        return Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices((context, services) =>
            {
                services.AddSingleton<IConfiguration>(configuration);
                services.AddInfrastructure(configuration);
                services.AddApplicationServices();
                services.AddViewModels();

                services.AddSingleton<MainWindow>();
                services.AddSingleton<ViewModels.Main.MainViewModel>();
            })
            .Build();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Fatal(e.Exception, "Unhandled UI exception");
        ShowErrorAndContinue(e.Exception);
        e.Handled = true;
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            Log.Fatal(ex, "Unhandled background exception (isTerminating={IsTerminating})", e.IsTerminating);
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unobserved task exception");
        e.SetObserved();
    }

    private static void ShowErrorAndContinue(Exception ex) =>
        MessageBox.Show(
            $"An unexpected error occurred:\n\n{ex.Message}\n\nThe application will continue running.",
            "Unexpected Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
}
