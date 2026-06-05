using Application.WPF.Models.Entities;
using Application.WPF.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using StrategyEntity = Application.WPF.Models.Entities.TradingStrategy;

namespace Application.WPF.ViewModels.Strategies;

public partial class StrategyRaterViewModel : ObservableObject
{
    private readonly ITradingStrategyService         _strategyService;
    private readonly ILogger<StrategyRaterViewModel> _logger;

    private int _strategyId;

    [ObservableProperty] private string _strategyTitle  = string.Empty;
    [ObservableProperty] private bool   _isBusy;
    [ObservableProperty] private string _generalError   = string.Empty;
    [ObservableProperty] private string _generalSuccess = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AverageRatingDisplay))]
    [NotifyPropertyChangedFor(nameof(HasRatings))]
    [NotifyPropertyChangedFor(nameof(IsQualified))]
    [NotifyPropertyChangedFor(nameof(QualityLabel))]
    [NotifyPropertyChangedFor(nameof(HasNoConfluences))]
    private ObservableCollection<StrategyRuleRatingItem> _confluences = new();

    public bool   HasNoConfluences    => !Confluences.Any();
    public bool   HasRatings          => Confluences.Any(c => c.IsRated);
    public bool   IsQualified         => AverageValue >= 6.5;
    public string QualityLabel        => IsQualified ? "✓ Setup calificado" : "✗ Setup no calificado";

    public double AverageValue
    {
        get
        {
            var rated = Confluences.Where(c => c.IsRated).ToList();
            return rated.Any() ? rated.Average(c => (double)c.Rating!.Value) : 0;
        }
    }

    public string AverageRatingDisplay
    {
        get
        {
            var rated = Confluences.Where(c => c.IsRated).ToList();
            return rated.Any() ? $"{rated.Average(c => (double)c.Rating!.Value):F1} / 10" : "—";
        }
    }

    public IReadOnlyList<int> RatingValues { get; } = Enumerable.Range(1, 10).ToList();

    public event EventHandler? CloseRequested;

    public StrategyRaterViewModel(
        ITradingStrategyService         strategyService,
        ILogger<StrategyRaterViewModel> logger)
    {
        _strategyService = strategyService;
        _logger          = logger;
    }

    public void LoadStrategy(StrategyEntity strategy)
    {
        _strategyId   = strategy.Id;
        StrategyTitle = strategy.Title;

        var items = strategy.Confluences
            .OrderBy(c => c.OrderIndex)
            .Select((c, i) => new StrategyRuleRatingItem
            {
                RuleId       = c.Id,
                Description  = c.Name,
                DisplayIndex = i + 1,
                Rating       = c.Rating
            })
            .ToList();

        Confluences = new ObservableCollection<StrategyRuleRatingItem>(items);

        foreach (var item in Confluences)
            item.PropertyChanged += (_, _) => RefreshAverages();
    }

    private void RefreshAverages()
    {
        OnPropertyChanged(nameof(AverageRatingDisplay));
        OnPropertyChanged(nameof(AverageValue));
        OnPropertyChanged(nameof(HasRatings));
        OnPropertyChanged(nameof(IsQualified));
        OnPropertyChanged(nameof(QualityLabel));
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        GeneralError = GeneralSuccess = string.Empty;
        IsBusy = true;
        try
        {
            var ratings = Confluences.ToDictionary(c => c.RuleId, c => c.Rating);
            await _strategyService.RateStrategyAsync(_strategyId, ratings);
            GeneralSuccess = "Calificación guardada correctamente.";
            await Task.Delay(800);
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rating strategy {Id}", _strategyId);
            GeneralError = "Ocurrió un error al guardar la calificación.";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, EventArgs.Empty);
}
