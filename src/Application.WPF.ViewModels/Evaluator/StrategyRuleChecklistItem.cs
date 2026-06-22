using CommunityToolkit.Mvvm.ComponentModel;

namespace Application.WPF.ViewModels.Evaluator;

/// <summary>One operational rule of a strategy, with a pass/fail checkbox for trade validation.</summary>
public partial class StrategyRuleChecklistItem : ObservableObject
{
    public int    RuleId       { get; init; }
    public string Description  { get; init; } = string.Empty;
    public int    DisplayIndex { get; init; }

    [ObservableProperty] private bool _isChecked;
}
