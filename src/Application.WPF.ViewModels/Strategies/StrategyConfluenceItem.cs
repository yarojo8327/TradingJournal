using CommunityToolkit.Mvvm.ComponentModel;

namespace Application.WPF.ViewModels.Strategies;

public partial class StrategyConfluenceItem : ObservableObject
{
    public int?   ExistingId   { get; set; }
    [ObservableProperty] private string _name       = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayIndex))]
    private int _orderIndex;
    public int DisplayIndex => OrderIndex + 1;
}
