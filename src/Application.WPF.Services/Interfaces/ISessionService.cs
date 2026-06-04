using Application.WPF.Models.Entities;

namespace Application.WPF.Services.Interfaces;

public interface ISessionService
{
    User? CurrentUser { get; }
    bool IsAuthenticated { get; }
    void SetUser(User user);
    void Clear();
    event EventHandler<User?>? SessionChanged;
}
