using Application.WPF.Models.Entities;
using Application.WPF.Services.Interfaces;
using Application.WPF.ViewModels;
using Application.WPF.ViewModels.Login;
using Application.WPF.ViewModels.Register;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Application.WPF.Tests.ViewModels;

public class LoginViewModelTests
{
    private readonly Mock<IUserService>       _userSvc    = new();
    private readonly Mock<ISessionService>    _session    = new();
    private readonly Mock<INavigationService> _navigation = new();
    private readonly LoginViewModel           _sut;

    public LoginViewModelTests()
    {
        _sut = new LoginViewModel(
            _userSvc.Object,
            _session.Object,
            _navigation.Object,
            NullLogger<LoginViewModel>.Instance);
    }

    // ── Constructor ──────────────────────────────────────────────────────

    [Fact]
    public void Constructor_SetsTitleCorrectly()
    {
        Assert.Equal("Iniciar sesión", _sut.Title);
    }

    // ── LoginCommand — validaciones ──────────────────────────────────────

    [Fact]
    public async Task LoginCommand_EmptyUsernameOrEmail_HasErrors()
    {
        _sut.UsernameOrEmail = string.Empty;
        _sut.Password        = "Pass@123";

        await _sut.LoginCommand.ExecuteAsync(null);

        Assert.True(_sut.HasErrors);
        _userSvc.Verify(s => s.AuthenticateAsync(
            It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task LoginCommand_EmptyPassword_SetsGeneralError()
    {
        _sut.UsernameOrEmail = "juan";
        _sut.Password        = string.Empty;

        await _sut.LoginCommand.ExecuteAsync(null);

        Assert.False(string.IsNullOrEmpty(_sut.GeneralError));
        _userSvc.Verify(s => s.AuthenticateAsync(
            It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    // ── LoginCommand — credenciales inválidas ────────────────────────────

    [Fact]
    public async Task LoginCommand_WithInvalidCredentials_SetsGeneralError()
    {
        _sut.UsernameOrEmail = "juan";
        _sut.Password        = "WrongPass!";
        _userSvc.Setup(s => s.AuthenticateAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((User?)null);

        await _sut.LoginCommand.ExecuteAsync(null);

        Assert.False(string.IsNullOrEmpty(_sut.GeneralError));
    }

    [Fact]
    public async Task LoginCommand_WithInvalidCredentials_DoesNotSetSession()
    {
        _sut.UsernameOrEmail = "juan";
        _sut.Password        = "WrongPass!";
        _userSvc.Setup(s => s.AuthenticateAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((User?)null);

        await _sut.LoginCommand.ExecuteAsync(null);

        _session.Verify(s => s.SetUser(It.IsAny<User>()), Times.Never);
    }

    // ── LoginCommand — credenciales válidas ──────────────────────────────

    [Fact]
    public async Task LoginCommand_WithValidCredentials_SetsSession()
    {
        var user = BuildUser();
        SetValidCredentials(user);

        await _sut.LoginCommand.ExecuteAsync(null);

        _session.Verify(s => s.SetUser(user), Times.Once);
    }

    [Fact]
    public async Task LoginCommand_WithValidCredentials_NavigatesToDashboard()
    {
        SetValidCredentials(BuildUser());

        await _sut.LoginCommand.ExecuteAsync(null);

        _navigation.Verify(n => n.NavigateTo<DashboardViewModel>(null), Times.Once);
    }

    [Fact]
    public async Task LoginCommand_WithValidCredentials_ClearsGeneralError()
    {
        SetValidCredentials(BuildUser());

        await _sut.LoginCommand.ExecuteAsync(null);

        Assert.Empty(_sut.GeneralError);
    }

    [Fact]
    public async Task LoginCommand_SetsIsBusyFalseWhenComplete()
    {
        SetValidCredentials(BuildUser());

        await _sut.LoginCommand.ExecuteAsync(null);

        Assert.False(_sut.IsBusy);
    }

    // ── GoToRegisterCommand ──────────────────────────────────────────────

    [Fact]
    public void GoToRegisterCommand_NavigatesToRegisterViewModel()
    {
        _sut.GoToRegisterCommand.Execute(null);

        _navigation.Verify(n => n.NavigateTo<RegisterViewModel>(null), Times.Once);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private void SetValidCredentials(User user)
    {
        _sut.UsernameOrEmail = "juan";
        _sut.Password        = "Pass@123";
        _userSvc.Setup(s => s.AuthenticateAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(user);
    }

    private static User BuildUser() => new()
    {
        Id = 1, FullName = "Juan García",
        Email = "juan@test.com", Username = "juan",
        PasswordHash = "hash", CreatedAt = DateTime.UtcNow
    };
}
