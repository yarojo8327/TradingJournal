using Application.WPF.Models.Entities;
using Application.WPF.Services.Interfaces;

namespace Application.WPF.Services.Session;

public class SessionService : ISessionService
{
    public User? CurrentUser { get; private set; }
    public bool IsAuthenticated => CurrentUser is not null;

    public event EventHandler<User?>? SessionChanged;

    public void SetUser(User user)
    {
        CurrentUser = user;
        SessionChanged?.Invoke(this, user);
    }

    public void Clear()
    {
        CurrentUser = null;
        SessionChanged?.Invoke(this, null);
    }
}
