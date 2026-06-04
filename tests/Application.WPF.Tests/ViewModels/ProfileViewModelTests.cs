using Application.WPF.Models.Entities;
using Application.WPF.Services.Interfaces;
using Application.WPF.ViewModels.Profile;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Application.WPF.Tests.ViewModels;

public class ProfileViewModelTests
{
    private readonly Mock<IUserService>    _userSvc  = new();
    private readonly Mock<ISessionService> _session  = new();
    private readonly Mock<IDialogService>  _dialog   = new();
    private readonly ProfileViewModel      _sut;

    public ProfileViewModelTests()
    {
        _sut = new ProfileViewModel(
            _userSvc.Object,
            _session.Object,
            _dialog.Object,
            NullLogger<ProfileViewModel>.Instance);
    }

    // ── InitializeAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task InitializeAsync_LoadsDataFromSession()
    {
        _session.Setup(s => s.CurrentUser).Returns(BuildUser());

        await _sut.InitializeAsync();

        Assert.Equal("Juan García",   _sut.FullName);
        Assert.Equal("juan@test.com", _sut.Email);
        Assert.Equal("juan",          _sut.Username);
    }

    [Fact]
    public async Task InitializeAsync_WhenNoUser_DoesNotThrow()
    {
        _session.Setup(s => s.CurrentUser).Returns((User?)null);

        var ex = await Record.ExceptionAsync(() => _sut.InitializeAsync());
        Assert.Null(ex);
    }

    [Fact]
    public async Task InitializeAsync_SetsMemberSinceDate()
    {
        _session.Setup(s => s.CurrentUser).Returns(BuildUser());

        await _sut.InitializeAsync();

        Assert.False(string.IsNullOrEmpty(_sut.MemberSince));
    }

    // ── SaveProfileCommand — validaciones ────────────────────────────────

    [Fact]
    public async Task SaveProfileCommand_EmptyFullName_HasErrors()
    {
        _session.Setup(s => s.CurrentUser).Returns(BuildUser());
        await _sut.InitializeAsync();
        _sut.FullName = string.Empty;

        await _sut.SaveProfileCommand.ExecuteAsync(null);

        Assert.True(_sut.HasErrors);
        _userSvc.Verify(s => s.UpdateProfileAsync(
            It.IsAny<int>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SaveProfileCommand_InvalidEmail_HasErrors()
    {
        _session.Setup(s => s.CurrentUser).Returns(BuildUser());
        await _sut.InitializeAsync();
        _sut.Email = "no-es-email";

        await _sut.SaveProfileCommand.ExecuteAsync(null);

        Assert.True(_sut.HasErrors);
    }

    // ── SaveProfileCommand — duplicados ──────────────────────────────────

    [Fact]
    public async Task SaveProfileCommand_WhenEmailTakenByOtherAccount_SetsProfileError()
    {
        var user = BuildUser();
        _session.Setup(s => s.CurrentUser).Returns(user);
        await _sut.InitializeAsync();
        _sut.Email = "otro@test.com";
        _userSvc.Setup(s => s.EmailExistsAsync("otro@test.com")).ReturnsAsync(true);

        await _sut.SaveProfileCommand.ExecuteAsync(null);

        Assert.False(string.IsNullOrEmpty(_sut.ProfileError));
    }

    [Fact]
    public async Task SaveProfileCommand_WhenUsernameTakenByOtherAccount_SetsProfileError()
    {
        var user = BuildUser();
        _session.Setup(s => s.CurrentUser).Returns(user);
        await _sut.InitializeAsync();
        _sut.Username = "otro_user";
        _userSvc.Setup(s => s.EmailExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        _userSvc.Setup(s => s.UsernameExistsAsync("otro_user")).ReturnsAsync(true);

        await _sut.SaveProfileCommand.ExecuteAsync(null);

        Assert.False(string.IsNullOrEmpty(_sut.ProfileError));
    }

    // ── SaveProfileCommand — éxito ───────────────────────────────────────

    [Fact]
    public async Task SaveProfileCommand_WithValidData_CallsUpdateProfile()
    {
        var user    = BuildUser();
        var updated = BuildUser();
        updated.FullName = "Juan Nuevo";
        _session.Setup(s => s.CurrentUser).Returns(user);
        await _sut.InitializeAsync();
        _sut.FullName = "Juan Nuevo";
        _userSvc.Setup(s => s.EmailExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        _userSvc.Setup(s => s.UsernameExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        _userSvc.Setup(s => s.UpdateProfileAsync(
            It.IsAny<int>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(updated);

        await _sut.SaveProfileCommand.ExecuteAsync(null);

        _userSvc.Verify(s => s.UpdateProfileAsync(
            user.Id, "Juan Nuevo", user.Email, user.Username), Times.Once);
    }

    [Fact]
    public async Task SaveProfileCommand_OnSuccess_SetsProfileSuccess()
    {
        var user = BuildUser();
        _session.Setup(s => s.CurrentUser).Returns(user);
        await _sut.InitializeAsync();
        _userSvc.Setup(s => s.EmailExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        _userSvc.Setup(s => s.UsernameExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        _userSvc.Setup(s => s.UpdateProfileAsync(
            It.IsAny<int>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(user);

        await _sut.SaveProfileCommand.ExecuteAsync(null);

        Assert.False(string.IsNullOrEmpty(_sut.ProfileSuccess));
    }

    [Fact]
    public async Task SaveProfileCommand_OnSuccess_UpdatesSession()
    {
        var user = BuildUser();
        _session.Setup(s => s.CurrentUser).Returns(user);
        await _sut.InitializeAsync();
        _userSvc.Setup(s => s.EmailExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        _userSvc.Setup(s => s.UsernameExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        _userSvc.Setup(s => s.UpdateProfileAsync(
            It.IsAny<int>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(user);

        await _sut.SaveProfileCommand.ExecuteAsync(null);

        _session.Verify(s => s.SetUser(user), Times.Once);
    }

    // ── ChangePasswordCommand ────────────────────────────────────────────

    [Fact]
    public async Task ChangePasswordCommand_EmptyCurrentPassword_SetsPasswordError()
    {
        _session.Setup(s => s.CurrentUser).Returns(BuildUser());
        await _sut.InitializeAsync();
        _sut.CurrentPassword    = string.Empty;
        _sut.NewPassword        = "NewPass@1";
        _sut.ConfirmNewPassword = "NewPass@1";

        await _sut.ChangePasswordCommand.ExecuteAsync(null);

        Assert.False(string.IsNullOrEmpty(_sut.PasswordError));
        _userSvc.Verify(s => s.ChangePasswordAsync(
            It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ChangePasswordCommand_WrongCurrentPassword_SetsPasswordError()
    {
        var user = BuildUser();
        _session.Setup(s => s.CurrentUser).Returns(user);
        await _sut.InitializeAsync();
        _sut.CurrentPassword    = "WrongPass!";
        _sut.NewPassword        = "NewPass@1";
        _sut.ConfirmNewPassword = "NewPass@1";
        _userSvc.Setup(s => s.AuthenticateAsync(user.Username, "WrongPass!"))
                .ReturnsAsync((User?)null);

        await _sut.ChangePasswordCommand.ExecuteAsync(null);

        Assert.False(string.IsNullOrEmpty(_sut.PasswordError));
        _userSvc.Verify(s => s.ChangePasswordAsync(
            It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ChangePasswordCommand_WithValidData_CallsChangePassword()
    {
        var user = BuildUser();
        _session.Setup(s => s.CurrentUser).Returns(user);
        await _sut.InitializeAsync();
        _sut.CurrentPassword    = "Current@1";
        _sut.NewPassword        = "NewPass@1";
        _sut.ConfirmNewPassword = "NewPass@1";
        _userSvc.Setup(s => s.AuthenticateAsync(user.Username, "Current@1"))
                .ReturnsAsync(user);

        await _sut.ChangePasswordCommand.ExecuteAsync(null);

        _userSvc.Verify(s => s.ChangePasswordAsync(user.Id, "NewPass@1"), Times.Once);
    }

    [Fact]
    public async Task ChangePasswordCommand_OnSuccess_SetsPasswordSuccess()
    {
        var user = BuildUser();
        _session.Setup(s => s.CurrentUser).Returns(user);
        await _sut.InitializeAsync();
        _sut.CurrentPassword    = "Current@1";
        _sut.NewPassword        = "NewPass@1";
        _sut.ConfirmNewPassword = "NewPass@1";
        _userSvc.Setup(s => s.AuthenticateAsync(user.Username, "Current@1"))
                .ReturnsAsync(user);

        await _sut.ChangePasswordCommand.ExecuteAsync(null);

        Assert.False(string.IsNullOrEmpty(_sut.PasswordSuccess));
    }

    [Fact]
    public async Task ChangePasswordCommand_OnSuccess_ClearsPasswordFields()
    {
        var user = BuildUser();
        _session.Setup(s => s.CurrentUser).Returns(user);
        await _sut.InitializeAsync();
        _sut.CurrentPassword    = "Current@1";
        _sut.NewPassword        = "NewPass@1";
        _sut.ConfirmNewPassword = "NewPass@1";
        _userSvc.Setup(s => s.AuthenticateAsync(user.Username, "Current@1"))
                .ReturnsAsync(user);

        await _sut.ChangePasswordCommand.ExecuteAsync(null);

        Assert.Empty(_sut.CurrentPassword);
        Assert.Empty(_sut.NewPassword);
        Assert.Empty(_sut.ConfirmNewPassword);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static User BuildUser() => new()
    {
        Id = 1, FullName = "Juan García",
        Email = "juan@test.com", Username = "juan",
        PasswordHash = BCrypt.Net.BCrypt.HashPassword("Pass@123"),
        CreatedAt = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc)
    };
}
