using Application.WPF.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Text.Json;

namespace Application.WPF.Services.Session;

public class SessionPersistenceService : ISessionPersistenceService
{
    private static readonly string SessionFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TradingJournal",
        "session.json");

    private readonly ILogger<SessionPersistenceService> _logger;

    public SessionPersistenceService(ILogger<SessionPersistenceService> logger)
    {
        _logger = logger;
    }

    public async Task SaveAsync(int userId)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SessionFilePath)!);
            var json = JsonSerializer.Serialize(new SessionData(userId));
            await File.WriteAllTextAsync(SessionFilePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo guardar la sesión persistente");
        }
    }

    public async Task ClearAsync()
    {
        try
        {
            if (File.Exists(SessionFilePath))
                await Task.Run(() => File.Delete(SessionFilePath));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo eliminar la sesión persistente");
        }
    }

    public async Task<int?> TryGetSavedUserIdAsync()
    {
        try
        {
            if (!File.Exists(SessionFilePath)) return null;

            var json = await File.ReadAllTextAsync(SessionFilePath);
            var data = JsonSerializer.Deserialize<SessionData>(json);
            return data?.UserId > 0 ? data.UserId : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo leer la sesión persistente");
            return null;
        }
    }

    private record SessionData(int UserId);
}
