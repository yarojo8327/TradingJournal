namespace Application.WPF.Models.Entities;

/// <summary>
/// Snapshot de la calificación de una confluencia dentro de un registro de Playbook.
/// Se almacena el nombre de la confluencia para que el registro sea válido incluso
/// si la estrategia cambia posteriormente.
/// </summary>
public class PlaybookConfluenceRating
{
    public int    Id              { get; set; }
    public int    PlaybookEntryId { get; set; }
    public int    ConfluenceId    { get; set; }    // referencia original (informativo)
    public string ConfluenceName  { get; set; } = string.Empty;  // snapshot del nombre
    public int    OrderIndex      { get; set; }
    public int    Rating          { get; set; }   // 1-10

    public PlaybookEntry PlaybookEntry { get; set; } = null!;
}
