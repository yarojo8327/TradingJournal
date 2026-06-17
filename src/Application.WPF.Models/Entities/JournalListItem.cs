namespace Application.WPF.Models.Entities;

public class JournalListItem
{
    public int    Id        { get; set; }
    public int    UserId    { get; set; }
    public string Category  { get; set; } = string.Empty;
    public string Name      { get; set; } = string.Empty;
    public int    SortOrder { get; set; }

    public User User { get; set; } = null!;
}
