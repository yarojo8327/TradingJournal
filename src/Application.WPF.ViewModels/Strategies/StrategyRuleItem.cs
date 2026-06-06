using CommunityToolkit.Mvvm.ComponentModel;

namespace Application.WPF.ViewModels.Strategies;

public partial class StrategyRuleItem : ObservableObject
{
    public int? ExistingId { get; set; }

    [ObservableProperty] private string _description = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayIndex))]
    private int _orderIndex;

    public int DisplayIndex => OrderIndex + 1;
}
