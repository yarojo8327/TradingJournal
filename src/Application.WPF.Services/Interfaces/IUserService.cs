using Application.WPF.Models.Entities;

namespace Application.WPF.Services.Interfaces;

public interface IUserService
{
    Task<bool> AnyUserExistsAsync();
    Task<bool> EmailExistsAsync(string email);
    Task<bool> UsernameExistsAsync(string username);
    Task<User> RegisterAsync(string fullName, string email, string username, string password);
    Task<User?> AuthenticateAsync(string usernameOrEmail, string password);
    Task<User> UpdateProfileAsync(int userId, string fullName, string email, string username);
    Task ChangePasswordAsync(int userId, string newPassword);
}
