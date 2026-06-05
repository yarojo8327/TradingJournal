namespace Application.WPF.Services.Interfaces;

public interface ISessionPersistenceService
{
    Task SaveAsync(int userId);
    Task ClearAsync();
    Task<int?> TryGetSavedUserIdAsync();
}
