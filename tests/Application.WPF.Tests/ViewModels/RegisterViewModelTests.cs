using Application.WPF.Models.Entities;
using Application.WPF.Services.Interfaces;
using Application.WPF.ViewModels;
using Application.WPF.ViewModels.Login;
using Application.WPF.ViewModels.Register;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Application.WPF.Tests.ViewModels;

public class RegisterViewModelTests
{
    private readonly Mock<IUserService>       _userSvc    = new();
    private readonly Mock<ISessionService>    _session    = new();
    private readonly Mock<INavigationService> _navigation = new();
    private readonly Mock<IDialogService>     _dialog     = new();
    private readonly RegisterViewModel        _sut;

    public RegisterViewModelTests()
    {
        _sut = new RegisterViewModel(
            _userSvc.Object,
            _session.Object,
            _navigation.Object,
            _dialog.Object,
            NullLogger<RegisterViewModel>.Instance);
    }

    // ── Constructor ──────────────────────────────────────────────────────

    [Fact]
    public void Constructor_SetsTitleCorrectly()
    {
        Assert.Equal("Registro de Usuario", _sut.Title);
    }

    // ── Validación de contraseña ─────────────────────────────────────────

    [Theory]
    [InlineData("short1A!",  true)]   // 8 chars — válida
    [InlineData("abc",       false)]  // muy corta
    [InlineData("alllower1!", false)] // sin mayúscula
    [InlineData("ALLUPPER1!", false)] // sin minúscula
    [InlineData("NoNumbers!", false)] // sin número
    [InlineData("NoSpecial1", false)] // sin carácter especial
    public void ValidatePasswordComplexity_ReturnsExpectedResult(string password, bool isValid)
    {
        var ctx    = new System.ComponentModel.DataAnnotations.ValidationContext(new object());
        var result = RegisterViewModel.ValidatePasswordComplexity(password, ctx);

        if (isValid)
            Assert.Equal(System.ComponentModel.DataAnnotations.ValidationResult.Success, result);
        else
            Assert.NotEqual(System.ComponentModel.DataAnnotations.ValidationResult.Success, result);
    }

    [Fact]
    public void ValidatePasswordComplexity_EmptyString_ReturnsSuccess()
    {
        var ctx    = new System.ComponentModel.DataAnnotations.ValidationContext(new object());
        var result = RegisterViewModel.ValidatePasswordComplexity(string.Empty, ctx);
        Assert.Equal(System.ComponentModel.DataAnnotations.ValidationResult.Success, result);
    }

    // ── RegisterCommand con datos inválidos ──────────────────────────────

    [Fact]
    public async Task RegisterCommand_EmptyFields_DoesNotCallService()
    {
        await _sut.RegisterCommand.ExecuteAsync(null);

        _userSvc.Verify(s => s.RegisterAsync(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RegisterCommand_InvalidEmail_HasErrors()
    {
        SetValidForm();
        _sut.Email = "no-es-un-email";

        await _sut.RegisterCommand.ExecuteAsync(null);

        Assert.True(_sut.HasErrors);
    }

    [Fact]
    public async Task RegisterCommand_PasswordMismatch_HasErrors()
    {
        SetValidForm();
        _sut.ConfirmPassword = "OtraClave@999";

        await _sut.RegisterCommand.ExecuteAsync(null);

        Assert.True(_sut.HasErrors);
    }

    // ── RegisterCommand con duplicados ───────────────────────────────────

    [Fact]
    public async Task RegisterCommand_WhenEmailTaken_SetsGeneralError()
    {
        SetValidForm();
        _userSvc.Setup(s => s.EmailExistsAsync(It.IsAny<string>())).ReturnsAsync(true);

        await _sut.RegisterCommand.ExecuteAsync(null);

        Assert.False(string.IsNullOrEmpty(_sut.GeneralError));
        _userSvc.Verify(s => s.RegisterAsync(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RegisterCommand_WhenUsernameTaken_SetsGeneralError()
    {
        SetValidForm();
        _userSvc.Setup(s => s.EmailExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        _userSvc.Setup(s => s.UsernameExistsAsync(It.IsAny<string>())).ReturnsAsync(true);

        await _sut.RegisterCommand.ExecuteAsync(null);

        Assert.False(string.IsNullOrEmpty(_sut.GeneralError));
    }

    // ── RegisterCommand exitoso ──────────────────────────────────────────

    [Fact]
    public async Task RegisterCommand_WithValidData_CallsRegisterAsync()
    {
        SetValidForm();
        var user = BuildUser();
        _userSvc.Setup(s => s.EmailExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        _userSvc.Setup(s => s.UsernameExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        _userSvc.Setup(s => s.RegisterAsync(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(user);

        await _sut.RegisterCommand.ExecuteAsync(null);

        _userSvc.Verify(s => s.RegisterAsync(
            "Juan García", "juan@test.com", "juan_trader", "Pass@1234"), Times.Once);
    }

    [Fact]
    public async Task RegisterCommand_WithValidData_InitiatesSession()
    {
        SetValidForm();
        var user = BuildUser();
        _userSvc.Setup(s => s.EmailExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        _userSvc.Setup(s => s.UsernameExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        _userSvc.Setup(s => s.RegisterAsync(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(user);

        await _sut.RegisterCommand.ExecuteAsync(null);

        _session.Verify(s => s.SetUser(user), Times.Once);
    }

    [Fact]
    public async Task RegisterCommand_WithValidData_NavigatesToDashboard()
    {
        SetValidForm();
        var user = BuildUser();
        _userSvc.Setup(s => s.EmailExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        _userSvc.Setup(s => s.UsernameExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        _userSvc.Setup(s => s.RegisterAsync(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(user);

        await _sut.RegisterCommand.ExecuteAsync(null);

        _navigation.Verify(n => n.NavigateTo<DashboardViewModel>(null), Times.Once);
    }

    // ── GoToLoginCommand ─────────────────────────────────────────────────

    [Fact]
    public void GoToLoginCommand_NavigatesToLoginViewModel()
    {
        _sut.GoToLoginCommand.Execute(null);

        _navigation.Verify(n => n.NavigateTo<LoginViewModel>(null), Times.Once);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private void SetValidForm()
    {
        _sut.FullName        = "Juan García";
        _sut.Email           = "juan@test.com";
        _sut.Username        = "juan_trader";
        _sut.Password        = "Pass@1234";
        _sut.ConfirmPassword = "Pass@1234";
    }

    private static User BuildUser() => new()
    {
        Id = 1, FullName = "Juan García",
        Email = "juan@test.com", Username = "juan_trader",
        PasswordHash = "hash", CreatedAt = DateTime.UtcNow
    };
}
