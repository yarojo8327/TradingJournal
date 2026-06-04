# TradingJournal

WPF desktop application built on .NET 7 with a production-ready MVVM enterprise architecture.

---

## Architecture

```
TradingJournal/
├── src/
│   ├── Application.WPF               # Entry point, Generic Host, DI composition root
│   ├── Application.WPF.Views         # XAML views only — no business logic
│   ├── Application.WPF.ViewModels    # Presentation logic, commands, navigation
│   ├── Application.WPF.Models        # DTOs, configuration models
│   ├── Application.WPF.Services      # NavigationService, DialogService, ConfigurationService
│   ├── Application.WPF.Infrastructure# Serilog setup, DI registration helpers
│   ├── Application.WPF.Common        # BaseViewModel, constants, extensions, helpers
│   └── Application.WPF.Resources     # Themes, styles, resource dictionaries
└── tests/
    └── Application.WPF.Tests         # xUnit tests (NavigationService, ViewModels, Commands)
```

### Dependency direction

```
Common ← Models ← Services ← ViewModels ← Views ← Application.WPF
              ↑
        Infrastructure
```

---

## Tech stack

| Concern | Library |
|---|---|
| Framework | .NET 7 / WPF |
| MVVM | CommunityToolkit.Mvvm 8.x |
| DI / lifecycle | Microsoft.Extensions.Hosting (Generic Host) |
| Logging | Serilog + Rolling File sink |
| Configuration | Microsoft.Extensions.Configuration + IOptions<T> |
| Testing | xUnit + Moq |

---

## Getting started

### Prerequisites
- .NET 7 SDK
- Windows 10/11

### Build
```bash
dotnet build TradingJournal.sln
```

### Run
```bash
dotnet run --project src/Application.WPF
```

### Test
```bash
dotnet test
```

### Set environment
```powershell
# Development mode (verbose logging, debug level)
$env:DOTNET_ENVIRONMENT = "Development"
dotnet run --project src/Application.WPF
```

---

## Key design decisions

### Generic Host
All lifecycle, DI container, logging and configuration are orchestrated through `IHost`. This gives a consistent startup/shutdown model and makes the app testable without a UI.

### ViewModel-first navigation
`NavigationService` resolves ViewModels from the DI container and fires a `Navigated` event. `MainWindow` uses a `ViewModelToViewConverter` to locate the matching XAML view by naming convention (`*ViewModel` → `*View`). Views never reference each other.

### BaseViewModel
Inherits `ObservableValidator` (CommunityToolkit.Mvvm) — provides `INotifyPropertyChanged`, `INotifyDataErrorInfo`, source-generated `[ObservableProperty]`, and `[RelayCommand]`.

### Strongly-typed configuration
`AppSettings` is bound via `IOptions<AppSettings>` and injected into `ConfigurationService`. No magic strings anywhere.

### Global exception handling
Three handlers in `App.xaml.cs`:
- `DispatcherUnhandledException` — UI thread (handled gracefully, app continues)
- `AppDomain.CurrentDomain.UnhandledException` — background threads
- `TaskScheduler.UnobservedTaskException` — unobserved async exceptions

---

## Adding a new screen

1. Create `MyFeatureViewModel` in `Application.WPF.ViewModels` inheriting `BaseViewModel`
2. Create `MyFeatureView.xaml` in `Application.WPF.Views` as `UserControl`
3. Register the ViewModel in `ViewModelsRegistration.cs`
4. Navigate: `_navigationService.NavigateTo<MyFeatureViewModel>()`

---

## Configuration files

| File | Purpose |
|---|---|
| `appsettings.json` | Base configuration |
| `appsettings.Development.json` | Overrides for dev (verbose logging) |
| `appsettings.Production.json` | Overrides for prod (warnings only) |

Set `DOTNET_ENVIRONMENT` env var to control which override loads.

---

## Themes

- `Themes/LightTheme.xaml` (default)
- `Themes/DarkTheme.xaml`

To switch at runtime, swap the merged dictionary in `AppResources.xaml` or implement a `ThemeService` that replaces the resource at `Application.Current.Resources` level.

---

## Logs

Rolling daily files at `logs/app-YYYYMMDD.log` relative to the executable, retaining 30 days.
