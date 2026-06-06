# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```powershell
# Build
dotnet build TradingJournal.sln

# Run all tests
dotnet test

# Run a single test class
dotnet test --filter "FullyQualifiedName~TradingStrategyServiceTests"

# Run with development environment (verbose logging)
$env:DOTNET_ENVIRONMENT = "Development"
dotnet run --project src/Application.WPF

# Clean before build (required after source generator changes or rename refactors)
dotnet clean && dotnet build TradingJournal.sln
```

**Always run `dotnet build` AND `dotnet test` after every change.**

## Architecture

Eight projects with a strict one-way dependency chain:

```
Common ← Models ← Services ← ViewModels ← Views ← Application.WPF
                ↑
          Infrastructure
```

- **`Application.WPF`** — Entry point. `App.xaml.cs` owns the Generic Host, bootstraps DI, registers `ILocalizationService` as `Application.Resources["Loc"]` for XAML bindings, and runs `EnsureSchemaUpToDateAsync` on startup.
- **`Application.WPF.Views`** — XAML-only UserControls. No business logic. Code-behind limited to UI-only wiring (e.g. `PasswordBox` text bridge, event handlers that call ViewModel methods). Views discover ViewModels via naming convention in `ViewModelToViewConverter` (`*ViewModel` → `*View`).
- **`Application.WPF.ViewModels`** — Presentation logic. All VMs inherit `BaseViewModel : ObservableValidator`. Use CommunityToolkit.Mvvm source generators (`[ObservableProperty]`, `[RelayCommand]`, `[NotifyDataErrorInfo]`).
- **`Application.WPF.Services`** — Application services (navigation, localization, session, CRUD services). Registered in `ServicesRegistration.cs`.
- **`Application.WPF.Infrastructure`** — `TradingJournalDbContext` (EF Core + SQLite, Transient lifetime), `InfrastructureServiceRegistration`.
- **`Application.WPF.Models`** — Entity classes and enums only.
- **`Application.WPF.Common`** — `BaseViewModel`, constants, RESX localization files.
- **`Application.WPF.Resources`** — XAML resource dictionaries (themes, styles).

## Key Design Patterns

### Navigation
`NavigationService` is a Singleton. Call `_navigationService.NavigateTo<TViewModel>()`. `MainWindow` subscribes to `Navigated` event and uses `ViewModelToViewConverter` to resolve the view by name. Views never reference each other.

### BaseViewModel
Inherits `ObservableValidator`. Provides `INotifyPropertyChanged`, `INotifyDataErrorInfo`, async `InitializeAsync()` virtual method, and `IsBusy`/`Title` base properties. **Do not declare `[ObservableProperty] private string _title`** in subclasses — it shadows `BaseViewModel.Title` and causes `StackOverflowException` via validation cascade. Use a different name (e.g. `_strategyTitle`).

### Database Schema Evolution
No EF migrations. The DB is created with `EnsureCreatedAsync()`. New tables are added via idempotent `CREATE TABLE IF NOT EXISTS` calls in `App.xaml.cs → EnsureSchemaUpToDateAsync()`. `TradingJournalDbContext` is Transient — each service call gets its own context.

When deleting child collections and re-inserting (e.g. rules/confluences on update), use `ExecuteSqlRawAsync` + `ChangeTracker.Clear()` to avoid stale tracked entity conflicts.

### Localization
- String resources: `src/Application.WPF.Common/Localization/Strings.resx` (English) and `Strings.es-CO.resx` (Spanish). All keys must exist in both files.
- `ILocalizationService` is a Singleton registered as `Application.Resources["Loc"]`.
- XAML usage: `xmlns:loc="clr-namespace:Application.WPF.Views.Localization"` → `Text="{loc:Tr MyKey}"`.
- `TrExtension` always creates a `Binding` and calls `binding.ProvideValue(serviceProvider)`. This returns a `BindingExpression` for normal controls, and the `Binding` itself (valid `BindingBase`) for `DataTemplate`/`Style Setter` contexts. Never return `this` from `ProvideValue` in a Setter context.
- Missing key renders as `[KeyName]` — visible in UI, easy to spot.
- Default language is `es-CO` (set in `ServicesRegistration.cs`).

### Session Persistence
`ISessionPersistenceService` saves `{UserId}` to `%AppData%\TradingJournal\session.json`. On splash screen, `SplashViewModel.InitializeAsync()` tries to restore the session. Manual logout calls `SessionService.Clear()` which deletes the file.

### Strategy Confluences & Rater
`TradingStrategy` has both `Rules` (operational rules) and `Confluences` (`StrategyConfluence` with optional `Rating` 1–10). `AverageRating`, `HasAverageRating`, and `IsQualifiedSetup` (threshold ≥ 6.5) are computed properties — marked `Ignore()` in EF model config.

`StrategyRaterViewModel` is injected as a factory `Func<StrategyRaterViewModel>` into `TradingStrategyViewModel` to avoid cross-assembly DI access from Views. The code-behind calls `listVm.CreateRater(strategy)` to get an initialized instance.

## Testing

Tests use SQLite in-memory via `SqliteConnection(":memory:")` with FK constraints enabled. Each test creates a seeded `TradingJournalDbContext` directly — no mocks for the DB layer. Services that depend on `ISessionService` or `ISessionPersistenceService` use inline stub implementations in the test class.

Run a specific test method:
```powershell
dotnet test --filter "FullyQualifiedName~TradingStrategyServiceTests.CreateAsync_PersistsConfluences"
```

## Branch Strategy

- `main` — production-ready, updated via PRs from `develop`
- `develop` — integration branch, all features merge here first
- `release` / `hotfix` — kept in sync with `develop` tip
- Feature branches follow `feature/<name>` and are deleted after merge
