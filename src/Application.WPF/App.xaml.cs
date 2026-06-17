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

    public static T GetService<T>() where T : notnull =>
        ((App)Current)._host!.Services.GetRequiredService<T>();

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

        // Ensure index is non-unique (multiple accounts per user allowed)
        await db.Database.ExecuteSqlRawAsync(
            @"DROP INDEX IF EXISTS ""IX_TradingAccounts_UserId"";");
        await db.Database.ExecuteSqlRawAsync(
            @"CREATE INDEX IF NOT EXISTS ""IX_TradingAccounts_UserId"" ON ""TradingAccounts"" (""UserId"");");

        await db.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS ""TradingStrategies"" (
                ""Id""            INTEGER NOT NULL CONSTRAINT ""PK_TradingStrategies"" PRIMARY KEY AUTOINCREMENT,
                ""UserId""        INTEGER NOT NULL,
                ""Title""         TEXT    NOT NULL,
                ""Description""   TEXT,
                ""ImageData""     BLOB,
                ""ImageMimeType"" TEXT,
                ""CreatedAt""     TEXT    NOT NULL,
                ""UpdatedAt""     TEXT,
                CONSTRAINT ""FK_TradingStrategies_Users_UserId""
                    FOREIGN KEY (""UserId"") REFERENCES ""Users"" (""Id"") ON DELETE CASCADE
            );");
        await db.Database.ExecuteSqlRawAsync(
            @"CREATE INDEX IF NOT EXISTS ""IX_TradingStrategies_UserId"" ON ""TradingStrategies"" (""UserId"");");

        await db.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS ""StrategyRules"" (
                ""Id""          INTEGER NOT NULL CONSTRAINT ""PK_StrategyRules"" PRIMARY KEY AUTOINCREMENT,
                ""StrategyId""  INTEGER NOT NULL,
                ""Description"" TEXT    NOT NULL,
                ""OrderIndex""  INTEGER NOT NULL,
                ""CreatedAt""   TEXT    NOT NULL,
                CONSTRAINT ""FK_StrategyRules_TradingStrategies_StrategyId""
                    FOREIGN KEY (""StrategyId"") REFERENCES ""TradingStrategies"" (""Id"") ON DELETE CASCADE
            );");
        await db.Database.ExecuteSqlRawAsync(
            @"CREATE INDEX IF NOT EXISTS ""IX_StrategyRules_StrategyId"" ON ""StrategyRules"" (""StrategyId"");");

        await db.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS ""StrategyConfluences"" (
                ""Id""         INTEGER NOT NULL CONSTRAINT ""PK_StrategyConfluences"" PRIMARY KEY AUTOINCREMENT,
                ""StrategyId"" INTEGER NOT NULL,
                ""Name""       TEXT    NOT NULL,
                ""OrderIndex"" INTEGER NOT NULL,
                ""Rating""     INTEGER,
                ""CreatedAt""  TEXT    NOT NULL,
                CONSTRAINT ""FK_StrategyConfluences_TradingStrategies_StrategyId""
                    FOREIGN KEY (""StrategyId"") REFERENCES ""TradingStrategies"" (""Id"") ON DELETE CASCADE
            );");
        await db.Database.ExecuteSqlRawAsync(
            @"CREATE INDEX IF NOT EXISTS ""IX_StrategyConfluences_StrategyId"" ON ""StrategyConfluences"" (""StrategyId"");");

        await db.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS ""TradeEntries"" (
                ""Id""               INTEGER NOT NULL CONSTRAINT ""PK_TradeEntries"" PRIMARY KEY AUTOINCREMENT,
                ""AccountId""        INTEGER NOT NULL,
                ""StrategyId""       INTEGER,
                ""Symbol""           TEXT    NOT NULL,
                ""Direction""        TEXT    NOT NULL,
                ""EntryDate""        TEXT    NOT NULL,
                ""ExitDate""         TEXT,
                ""EntryPrice""       TEXT    NOT NULL,
                ""ExitPrice""        TEXT,
                ""StopLoss""         TEXT,
                ""TakeProfit""       TEXT,
                ""PositionSizeLots"" TEXT,
                ""RiskAmount""       TEXT,
                ""ProfitLoss""       TEXT,
                ""PipsResult""       TEXT,
                ""RiskRewardRatio""  TEXT,
                ""Result""           TEXT    NOT NULL DEFAULT 'Open',
                ""Session""          TEXT,
                ""Timeframe""        TEXT,
                ""SetupQuality""     INTEGER,
                ""ConfluencesCount"" INTEGER,
                ""IsFalseBreakout""  INTEGER NOT NULL DEFAULT 0,
                ""Rating""           INTEGER,
                ""EmotionalState""   TEXT,
                ""MistakeType""      TEXT,
                ""Notes""            TEXT,
                ""ScreenshotUrl""    TEXT,
                ""CreatedAt""        TEXT    NOT NULL,
                ""UpdatedAt""        TEXT,
                CONSTRAINT ""FK_TradeEntries_TradingAccounts_AccountId""
                    FOREIGN KEY (""AccountId"") REFERENCES ""TradingAccounts"" (""Id"") ON DELETE CASCADE,
                CONSTRAINT ""FK_TradeEntries_TradingStrategies_StrategyId""
                    FOREIGN KEY (""StrategyId"") REFERENCES ""TradingStrategies"" (""Id"") ON DELETE SET NULL
            );");
        await db.Database.ExecuteSqlRawAsync(
            @"CREATE INDEX IF NOT EXISTS ""IX_TradeEntries_AccountId"" ON ""TradeEntries"" (""AccountId"");");
        await db.Database.ExecuteSqlRawAsync(
            @"CREATE INDEX IF NOT EXISTS ""IX_TradeEntries_StrategyId"" ON ""TradeEntries"" (""StrategyId"");");

        await db.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS ""PlaybookEntries"" (
                ""Id""         INTEGER NOT NULL CONSTRAINT ""PK_PlaybookEntries"" PRIMARY KEY AUTOINCREMENT,
                ""UserId""     INTEGER NOT NULL,
                ""StrategyId"" INTEGER,
                ""Title""      TEXT    NOT NULL,
                ""Notes""      TEXT,
                ""ImageUrl""   TEXT,
                ""Rating""     REAL,
                ""CreatedAt""  TEXT    NOT NULL,
                ""UpdatedAt""  TEXT,
                CONSTRAINT ""FK_PlaybookEntries_Users_UserId""
                    FOREIGN KEY (""UserId"") REFERENCES ""Users"" (""Id"") ON DELETE CASCADE,
                CONSTRAINT ""FK_PlaybookEntries_TradingStrategies_StrategyId""
                    FOREIGN KEY (""StrategyId"") REFERENCES ""TradingStrategies"" (""Id"") ON DELETE SET NULL
            );");
        await db.Database.ExecuteSqlRawAsync(
            @"CREATE INDEX IF NOT EXISTS ""IX_PlaybookEntries_UserId"" ON ""PlaybookEntries"" (""UserId"");");

        await db.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS ""PlaybookConfluenceRatings"" (
                ""Id""              INTEGER NOT NULL CONSTRAINT ""PK_PlaybookConfluenceRatings"" PRIMARY KEY AUTOINCREMENT,
                ""PlaybookEntryId"" INTEGER NOT NULL,
                ""ConfluenceId""    INTEGER NOT NULL,
                ""ConfluenceName""  TEXT    NOT NULL,
                ""OrderIndex""      INTEGER NOT NULL,
                ""Rating""          INTEGER NOT NULL,
                CONSTRAINT ""FK_PlaybookConfluenceRatings_PlaybookEntries_PlaybookEntryId""
                    FOREIGN KEY (""PlaybookEntryId"") REFERENCES ""PlaybookEntries"" (""Id"") ON DELETE CASCADE
            );");
        await db.Database.ExecuteSqlRawAsync(
            @"CREATE INDEX IF NOT EXISTS ""IX_PlaybookConfluenceRatings_PlaybookEntryId"" ON ""PlaybookConfluenceRatings"" (""PlaybookEntryId"");");

        await db.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS ""JournalListItems"" (
                ""Id""        INTEGER NOT NULL CONSTRAINT ""PK_JournalListItems"" PRIMARY KEY AUTOINCREMENT,
                ""UserId""    INTEGER NOT NULL,
                ""Category""  TEXT    NOT NULL,
                ""Name""      TEXT    NOT NULL,
                ""SortOrder"" INTEGER NOT NULL DEFAULT 0,
                CONSTRAINT ""FK_JournalListItems_Users_UserId""
                    FOREIGN KEY (""UserId"") REFERENCES ""Users"" (""Id"") ON DELETE CASCADE
            );");
        await db.Database.ExecuteSqlRawAsync(
            @"CREATE INDEX IF NOT EXISTS ""IX_JournalListItems_UserId_Category"" ON ""JournalListItems"" (""UserId"", ""Category"");");

        // Columnas agregadas después de la creación inicial — idempotentes vía try/catch
        // (SQLite no soporta ADD COLUMN IF NOT EXISTS)
        await db.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS ""SymbolMappings"" (
                ""Id""            INTEGER NOT NULL CONSTRAINT ""PK_SymbolMappings"" PRIMARY KEY AUTOINCREMENT,
                ""BrokerSymbol""  TEXT    NOT NULL,
                ""CanonicalName"" TEXT    NOT NULL,
                ""Category""      TEXT    NOT NULL DEFAULT 'Other'
            );");
        await db.Database.ExecuteSqlRawAsync(
            @"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_SymbolMappings_BrokerSymbol"" ON ""SymbolMappings"" (""BrokerSymbol"");");

        await TryAddColumnAsync(db, @"ALTER TABLE ""TradeEntries"" ADD COLUMN ""Rating"" INTEGER;");
        await TryAddColumnAsync(db, @"ALTER TABLE ""TradeEntries"" ADD COLUMN ""TradingType"" TEXT;");
        await TryAddColumnAsync(db, @"ALTER TABLE ""PlaybookEntries"" ADD COLUMN ""ManualRating"" INTEGER;");
        await TryAddColumnAsync(db, @"ALTER TABLE ""PlaybookEntries"" ADD COLUMN ""ImageData"" BLOB;");
        await TryAddColumnAsync(db, @"ALTER TABLE ""PlaybookEntries"" ADD COLUMN ""ImageMimeType"" TEXT;");
    }

    private static async Task TryAddColumnAsync(TradingJournalDbContext db, string alterSql)
    {
        try   { await db.Database.ExecuteSqlRawAsync(alterSql); }
        catch { /* columna ya existe — ignorar */ }
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

    private static bool _showingError;
    private static void ShowErrorAndContinue(Exception ex)
    {
        if (_showingError) return;
        _showingError = true;
        try
        {
            MessageBox.Show(
                $"An unexpected error occurred:\n\n{ex.Message}\n\nThe application will continue running.",
                "Unexpected Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            _showingError = false;
        }
    }
}
