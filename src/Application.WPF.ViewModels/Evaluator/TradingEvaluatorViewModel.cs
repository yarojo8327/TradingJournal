using Application.WPF.Common.ViewModels;
using Application.WPF.Services.Interfaces;
using Application.WPF.ViewModels.LotCalculator;
using Application.WPF.ViewModels.Strategies;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media.Imaging;
using StrategyEntity = Application.WPF.Models.Entities.TradingStrategy;

namespace Application.WPF.ViewModels.Evaluator;

/// <summary>
/// Evaluador de Trading: combina selección de estrategia + gráfico asociado,
/// calificador de confluencias y calculadora de lotaje en una sola pantalla,
/// para evaluar un setup de principio a fin antes de operar.
/// </summary>
public partial class TradingEvaluatorViewModel : BaseViewModel
{
    private readonly ITradingStrategyService      _strategyService;
    private readonly ISessionService              _sessionService;
    private readonly Func<StrategyRaterViewModel> _raterFactory;

    public LotCalculatorViewModel LotCalculator { get; }

    public TradingEvaluatorViewModel(
        ITradingStrategyService       strategyService,
        ISessionService               sessionService,
        Func<StrategyRaterViewModel>  raterFactory,
        LotCalculatorViewModel        lotCalculatorViewModel)
    {
        _strategyService = strategyService;
        _sessionService  = sessionService;
        _raterFactory    = raterFactory;
        LotCalculator    = lotCalculatorViewModel;
        Title            = "Evaluador de Trading";
    }

    // ── Catálogo de estrategias ──────────────────────────────────────────

    [ObservableProperty] private ObservableCollection<StrategyEntity> _strategies = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStrategySelected))]
    [NotifyPropertyChangedFor(nameof(HasNoStrategySelected))]
    private StrategyEntity? _selectedStrategy;

    public bool HasStrategySelected   => SelectedStrategy is not null;
    public bool HasNoStrategySelected => SelectedStrategy is null;

    // ── Gráfico de la estrategia ──────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoChartImage))]
    private BitmapImage? _chartImage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoChartImage))]
    private bool _hasChartImage;

    public bool HasNoChartImage => !HasChartImage;

    // ── Calificador de confluencias (panel embebido) ──────────────────────

    [ObservableProperty] private StrategyRaterViewModel? _rater;

    // ── Checklist de reglas (todas deben cumplirse para que el trade sea válido) ──

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRules))]
    [NotifyPropertyChangedFor(nameof(HasNoRules))]
    [NotifyPropertyChangedFor(nameof(IsValidSetup))]
    [NotifyPropertyChangedFor(nameof(RulesCheckedCount))]
    [NotifyPropertyChangedFor(nameof(RulesProgressDisplay))]
    private ObservableCollection<StrategyRuleChecklistItem> _ruleChecklist = new();

    public bool   HasRules           => RuleChecklist.Any();
    public bool   HasNoRules         => !RuleChecklist.Any();
    public int    RulesCheckedCount  => RuleChecklist.Count(r => r.IsChecked);
    public bool   IsValidSetup       => RuleChecklist.Any() && RuleChecklist.All(r => r.IsChecked);
    public string RulesProgressDisplay => $"{RulesCheckedCount}/{RuleChecklist.Count}";

    public override async Task InitializeAsync()
    {
        var user = _sessionService.CurrentUser;
        if (user is null) return;

        var list = await _strategyService.GetAllByUserIdAsync(user.Id);
        Strategies = new ObservableCollection<StrategyEntity>(list);
        if (SelectedStrategy is null && Strategies.Count > 0)
            SelectedStrategy = Strategies[0];

        await LotCalculator.InitializeAsync();
    }

    partial void OnSelectedStrategyChanged(StrategyEntity? value)
    {
        if (value is null)
        {
            ChartImage     = null;
            HasChartImage  = false;
            Rater          = null;
            RuleChecklist  = new ObservableCollection<StrategyRuleChecklistItem>();
            return;
        }

        SetChartImage(value.ImageData);

        var rater = _raterFactory();
        rater.LoadStrategy(value);
        Rater = rater;

        LoadRuleChecklist(value);
    }

    private void LoadRuleChecklist(StrategyEntity strategy)
    {
        var items = strategy.Rules
            .OrderBy(r => r.OrderIndex)
            .Select((r, i) => new StrategyRuleChecklistItem
            {
                RuleId       = r.Id,
                Description  = r.Description,
                DisplayIndex = i + 1
            })
            .ToList();

        foreach (var item in items)
            item.PropertyChanged += (_, _) => RefreshChecklistState();

        RuleChecklist = new ObservableCollection<StrategyRuleChecklistItem>(items);
    }

    private void RefreshChecklistState()
    {
        OnPropertyChanged(nameof(RulesCheckedCount));
        OnPropertyChanged(nameof(IsValidSetup));
        OnPropertyChanged(nameof(RulesProgressDisplay));
    }

    private void SetChartImage(byte[]? data)
    {
        if (data is not { Length: > 0 })
        {
            ChartImage    = null;
            HasChartImage = false;
            return;
        }

        try
        {
            var img = new BitmapImage();
            using var ms = new MemoryStream(data);
            img.BeginInit();
            img.CacheOption  = BitmapCacheOption.OnLoad;
            img.StreamSource = ms;
            img.EndInit();
            img.Freeze();
            ChartImage    = img;
            HasChartImage = true;
        }
        catch
        {
            ChartImage    = null;
            HasChartImage = false;
        }
    }
}
