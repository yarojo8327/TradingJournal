using CommunityToolkit.Mvvm.ComponentModel;

namespace Application.WPF.ViewModels.Playbook;

public partial class StarRatingItem : ObservableObject
{
    public int Value { get; init; }

    [ObservableProperty]
    private bool _isFilled;
}
