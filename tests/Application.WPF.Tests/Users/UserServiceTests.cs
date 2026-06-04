using Application.WPF.Infrastructure.Data;
using Application.WPF.Models.Entities;
using Application.WPF.Services.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Application.WPF.Tests.Users;

public class UserServiceTests : IDisposable
{
    private readonly TradingJournalDbContext _db;
    private readonly UserService _sut;

    public UserServiceTests()
    {
        var options = new DbContextOptionsBuilder<TradingJournalDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db  = new TradingJournalDbContext(options);
        _sut = new UserService(_db, NullLogger<UserService>.Instance);
    }

    // ── AnyUserExistsAsync ───────────────────────────────────────────────

    [Fact]
    public async Task AnyUserExistsAsync_WhenNoUsers_ReturnsFalse()
    {
        var result = await _sut.AnyUserExistsAsync();
        Assert.False(result);
    }

    [Fact]
    public async Task AnyUserExistsAsync_WhenUserExists_ReturnsTrue()
    {
        await _sut.RegisterAsync("Juan García", "juan@test.com", "juan", "Pass@123");

        var result = await _sut.AnyUserExistsAsync();
        Assert.True(result);
    }

    // ── EmailExistsAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task EmailExistsAsync_WhenEmailDoesNotExist_ReturnsFalse()
    {
        var result = await _sut.EmailExistsAsync("noexiste@test.com");
        Assert.False(result);
    }

    [Fact]
    public async Task EmailExistsAsync_WhenEmailExists_ReturnsTrue()
    {
        await _sut.RegisterAsync("Juan", "juan@test.com", "juan", "Pass@123");

        var result = await _sut.EmailExistsAsync("juan@test.com");
        Assert.True(result);
    }

    [Fact]
    public async Task EmailExistsAsync_IsCaseInsensitive()
    {
        await _sut.RegisterAsync("Juan", "juan@test.com", "juan", "Pass@123");

        var result = await _sut.EmailExistsAsync("JUAN@TEST.COM");
        Assert.True(result);
    }

    // ── UsernameExistsAsync ──────────────────────────────────────────────

    [Fact]
    public async Task UsernameExistsAsync_WhenUsernameDoesNotExist_ReturnsFalse()
    {
        var result = await _sut.UsernameExistsAsync("noexiste");
        Assert.False(result);
    }

    [Fact]
    public async Task UsernameExistsAsync_WhenUsernameExists_ReturnsTrue()
    {
        await _sut.RegisterAsync("Juan", "juan@test.com", "juan", "Pass@123");

        var result = await _sut.UsernameExistsAsync("juan");
        Assert.True(result);
    }

    // ── RegisterAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task RegisterAsync_ReturnsUserWithCorrectData()
    {
        var user = await _sut.RegisterAsync("Juan García", "juan@test.com", "juan", "Pass@123");

        Assert.Equal("Juan García", user.FullName);
        Assert.Equal("juan@test.com", user.Email);
        Assert.Equal("juan", user.Username);
    }

    [Fact]
    public async Task RegisterAsync_NormalizesEmailToLowercase()
    {
        var user = await _sut.RegisterAsync("Juan", "JUAN@TEST.COM", "juan", "Pass@123");

        Assert.Equal("juan@test.com", user.Email);
    }

    [Fact]
    public async Task RegisterAsync_HashesPassword()
    {
        var user = await _sut.RegisterAsync("Juan", "juan@test.com", "juan", "Pass@123");

        Assert.NotEqual("Pass@123", user.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify("Pass@123", user.PasswordHash));
    }

    [Fact]
    public async Task RegisterAsync_SetsCreatedAt()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var user   = await _sut.RegisterAsync("Juan", "juan@test.com", "juan", "Pass@123");
        var after  = DateTime.UtcNow.AddSeconds(1);

        Assert.InRange(user.CreatedAt, before, after);
    }

    [Fact]
    public async Task RegisterAsync_PersistsUserToDatabase()
    {
        await _sut.RegisterAsync("Juan", "juan@test.com", "juan", "Pass@123");

        Assert.Equal(1, await _db.Users.CountAsync());
    }

    // ── AuthenticateAsync ────────────────────────────────────────────────

    [Fact]
    public async Task AuthenticateAsync_WithUnknownUser_ReturnsNull()
    {
        var result = await _sut.AuthenticateAsync("noexiste", "Pass@123");
        Assert.Null(result);
    }

    [Fact]
    public async Task AuthenticateAsync_WithWrongPassword_ReturnsNull()
    {
        await _sut.RegisterAsync("Juan", "juan@test.com", "juan", "Pass@123");

        var result = await _sut.AuthenticateAsync("juan", "WrongPass!");
        Assert.Null(result);
    }

    [Fact]
    public async Task AuthenticateAsync_WithValidUsername_ReturnsUser()
    {
        await _sut.RegisterAsync("Juan", "juan@test.com", "juan", "Pass@123");

        var result = await _sut.AuthenticateAsync("juan", "Pass@123");
        Assert.NotNull(result);
        Assert.Equal("juan", result!.Username);
    }

    [Fact]
    public async Task AuthenticateAsync_WithValidEmail_ReturnsUser()
    {
        await _sut.RegisterAsync("Juan", "juan@test.com", "juan", "Pass@123");

        var result = await _sut.AuthenticateAsync("juan@test.com", "Pass@123");
        Assert.NotNull(result);
    }

    [Fact]
    public async Task AuthenticateAsync_EmailLookupIsCaseInsensitive()
    {
        await _sut.RegisterAsync("Juan", "juan@test.com", "juan", "Pass@123");

        var result = await _sut.AuthenticateAsync("JUAN@TEST.COM", "Pass@123");
        Assert.NotNull(result);
    }

    // ── UpdateProfileAsync ───────────────────────────────────────────────

    [Fact]
    public async Task UpdateProfileAsync_UpdatesUserFields()
    {
        var user = await _sut.RegisterAsync("Juan", "juan@test.com", "juan", "Pass@123");

        var updated = await _sut.UpdateProfileAsync(user.Id, "Juan Actualizado", "nuevo@test.com", "juan2");

        Assert.Equal("Juan Actualizado", updated.FullName);
        Assert.Equal("nuevo@test.com",   updated.Email);
        Assert.Equal("juan2",            updated.Username);
    }

    [Fact]
    public async Task UpdateProfileAsync_WithInvalidId_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.UpdateProfileAsync(999, "X", "x@x.com", "x"));
    }

    // ── ChangePasswordAsync ──────────────────────────────────────────────

    [Fact]
    public async Task ChangePasswordAsync_UpdatesPasswordHash()
    {
        var user = await _sut.RegisterAsync("Juan", "juan@test.com", "juan", "Pass@123");

        await _sut.ChangePasswordAsync(user.Id, "NewPass@456");

        var refreshed = await _db.Users.FindAsync(user.Id);
        Assert.True(BCrypt.Net.BCrypt.Verify("NewPass@456", refreshed!.PasswordHash));
    }

    [Fact]
    public async Task ChangePasswordAsync_WithInvalidId_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.ChangePasswordAsync(999, "NewPass@456"));
    }

    public void Dispose() => _db.Dispose();
}
