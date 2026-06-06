using CommunityToolkit.Mvvm.ComponentModel;

namespace Application.WPF.ViewModels.Playbook;

public partial class PlaybookConfluenceItem : ObservableObject
{
    public int    ConfluenceId   { get; set; }
    public string ConfluenceName { get; set; } = string.Empty;
    public int    OrderIndex     { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRated))]
    private int? _rating;

    public bool IsRated => Rating.HasValue;

    public static IReadOnlyList<int> RatingValues { get; } = Enumerable.Range(1, 10).ToList();
}
