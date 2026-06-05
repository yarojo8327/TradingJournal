using Application.WPF.Models.Entities;
using Application.WPF.Services.Interfaces;
using Application.WPF.Services.Session;
using Xunit;

namespace Application.WPF.Tests.Session;

public class SessionServiceTests
{
    // Stub que no persiste nada (tests unitarios, sin disco)
    private sealed class NoPersistence : ISessionPersistenceService
    {
        public Task SaveAsync(int userId)          => Task.CompletedTask;
        public Task ClearAsync()                   => Task.CompletedTask;
        public Task<int?> TryGetSavedUserIdAsync() => Task.FromResult<int?>(null);
    }

    private readonly SessionService _sut = new(new NoPersistence());

    [Fact]
    public void IsAuthenticated_WhenNoUser_ReturnsFalse()
    {
        Assert.False(_sut.IsAuthenticated);
    }

    [Fact]
    public void CurrentUser_WhenNoUser_ReturnsNull()
    {
        Assert.Null(_sut.CurrentUser);
    }

    [Fact]
    public void SetUser_SetsCurrentUser()
    {
        var user = BuildUser();
        _sut.SetUser(user);

        Assert.Same(user, _sut.CurrentUser);
    }

    [Fact]
    public void SetUser_SetsIsAuthenticatedTrue()
    {
        _sut.SetUser(BuildUser());
        Assert.True(_sut.IsAuthenticated);
    }

    [Fact]
    public void SetUser_RaisesSessionChangedWithUser()
    {
        User? received = null;
        _sut.SessionChanged += (_, u) => received = u;
        var user = BuildUser();

        _sut.SetUser(user);

        Assert.Same(user, received);
    }

    [Fact]
    public void Clear_RemovesCurrentUser()
    {
        _sut.SetUser(BuildUser());
        _sut.Clear();

        Assert.Null(_sut.CurrentUser);
    }

    [Fact]
    public void Clear_SetsIsAuthenticatedFalse()
    {
        _sut.SetUser(BuildUser());
        _sut.Clear();

        Assert.False(_sut.IsAuthenticated);
    }

    [Fact]
    public void Clear_RaisesSessionChangedWithNull()
    {
        _sut.SetUser(BuildUser());
        User? received = BuildUser();
        _sut.SessionChanged += (_, u) => received = u;

        _sut.Clear();

        Assert.Null(received);
    }

    private static User BuildUser() => new()
    {
        Id           = 1,
        FullName     = "Juan García",
        Email        = "juan@test.com",
        Username     = "juan",
        PasswordHash = "hash",
        CreatedAt    = DateTime.UtcNow
    };
}
