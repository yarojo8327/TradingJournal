using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Application.WPF.ViewModels.Strategies;

public partial class StrategyRuleRatingItem : ObservableObject
{
    public int    RuleId       { get; init; }
    public string Description  { get; init; } = string.Empty;
    public int    DisplayIndex { get; init; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RatingDisplay))]
    [NotifyPropertyChangedFor(nameof(IsRated))]
    private int? _rating;

    public bool   IsRated       => Rating.HasValue;
    public string RatingDisplay => Rating.HasValue ? $"{Rating}/10" : "Sin calificar";

    /// <summary>Establece o limpia el rating al hacer clic en un botón 1-10.</summary>
    [RelayCommand]
    private void SetRating(int value)
    {
        // Segundo clic en el mismo valor lo limpia
        Rating = Rating == value ? null : value;
    }

    /// <summary>Indica si el valor dado es el rating actualmente seleccionado.</summary>
    public bool IsSelected(int value) => Rating == value;
}
