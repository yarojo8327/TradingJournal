using Application.WPF.Infrastructure.Data;
using Application.WPF.Models.Entities;
using Application.WPF.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.WPF.Services.Users;

public class UserService : IUserService
{
    private readonly TradingJournalDbContext _db;
    private readonly ILogger<UserService> _logger;

    public UserService(TradingJournalDbContext db, ILogger<UserService> logger)
    {
        _db     = db;
        _logger = logger;
    }

    public async Task<bool> AnyUserExistsAsync() =>
        await _db.Users.AnyAsync();

    public async Task<User?> GetByIdAsync(int userId) =>
        await _db.Users.FindAsync(userId);

    public async Task<bool> EmailExistsAsync(string email) =>
        await _db.Users.AnyAsync(u => u.Email == email.Trim().ToLower());

    public async Task<bool> UsernameExistsAsync(string username) =>
        await _db.Users.AnyAsync(u => u.Username.ToLower() == username.Trim().ToLower());

    public async Task<User> RegisterAsync(string fullName, string email, string username, string password)
    {
        var user = new User
        {
            FullName     = fullName.Trim(),
            Email        = email.Trim().ToLower(),
            Username     = username.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            CreatedAt    = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        _logger.LogInformation("User registered: {Username}", user.Username);
        return user;
    }

    public async Task<User?> AuthenticateAsync(string usernameOrEmail, string password)
    {
        var input = usernameOrEmail.Trim().ToLower();
        var user  = await _db.Users.FirstOrDefaultAsync(
            u => u.Email == input || u.Username.ToLower() == input);

        if (user is null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return null;

        _logger.LogInformation("User authenticated: {Username}", user.Username);
        return user;
    }

    public async Task<User> UpdateProfileAsync(int userId, string fullName, string email, string username)
    {
        var user = await _db.Users.FindAsync(userId)
            ?? throw new InvalidOperationException("Usuario no encontrado.");

        user.FullName = fullName.Trim();
        user.Email    = email.Trim().ToLower();
        user.Username = username.Trim();

        await _db.SaveChangesAsync();
        _logger.LogInformation("Profile updated for user {Id}", userId);
        return user;
    }

    public async Task ChangePasswordAsync(int userId, string newPassword)
    {
        var user = await _db.Users.FindAsync(userId)
            ?? throw new InvalidOperationException("Usuario no encontrado.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Password changed for user {Id}", userId);
    }
}
