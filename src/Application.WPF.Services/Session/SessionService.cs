using Application.WPF.Models.Entities;
using Application.WPF.Services.Interfaces;

namespace Application.WPF.Services.Session;

public class SessionService : ISessionService
{
    private readonly ISessionPersistenceService _persistence;

    public User? CurrentUser { get; private set; }
    public bool IsAuthenticated => CurrentUser is not null;

    public event EventHandler<User?>? SessionChanged;

    public SessionService(ISessionPersistenceService persistence)
    {
        _persistence = persistence;
    }

    public void SetUser(User user)
    {
        CurrentUser = user;
        SessionChanged?.Invoke(this, user);
        _ = _persistence.SaveAsync(user.Id);
    }

    public void Clear()
    {
        CurrentUser = null;
        SessionChanged?.Invoke(this, null);
        _ = _persistence.ClearAsync();
    }
}
