using Application.WPF.Models.Entities;
using Application.WPF.Models.Enums;
using Application.WPF.Services.Interfaces;
using Application.WPF.ViewModels.TradingAccount;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Application.WPF.Tests.ViewModels;

public class TradingAccountViewModelTests
{
    private readonly Mock<ITradingAccountService> _accountSvc = new();
    private readonly Mock<ISessionService>        _session    = new();
    private readonly TradingAccountViewModel      _sut;

    private static readonly User SampleUser = new()
    {
        Id = 1, FullName = "Juan García", Email = "juan@test.com",
        Username = "juan", PasswordHash = "hash", CreatedAt = DateTime.UtcNow
    };

    private static readonly TradingAccount SampleAccount = new()
    {
        Id = 10, UserId = 1, Broker = "Pepperstone", AccountNumber = "ACC123",
        AccountType = AccountType.Real, InitialCapital = 10000m, BaseCurrency = "USD",
        Leverage = "1:200", StartDate = new DateTime(2025, 1, 1), CreatedAt = DateTime.UtcNow
    };

    public TradingAccountViewModelTests()
    {
        _sut = new TradingAccountViewModel(
            _accountSvc.Object,
            _session.Object,
            NullLogger<TradingAccountViewModel>.Instance);
    }

    // ── InitializeAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task InitializeAsync_WhenNoAccount_IsEditModeFalse()
    {
        _session.Setup(s => s.CurrentUser).Returns(SampleUser);
        _accountSvc.Setup(s => s.GetByUserIdAsync(1)).ReturnsAsync((TradingAccount?)null);

        await _sut.InitializeAsync();

        Assert.False(_sut.IsEditMode);
    }

    [Fact]
    public async Task InitializeAsync_WhenAccountExists_LoadsData()
    {
        _session.Setup(s => s.CurrentUser).Returns(SampleUser);
        _accountSvc.Setup(s => s.GetByUserIdAsync(1)).ReturnsAsync(SampleAccount);

        await _sut.InitializeAsync();

        Assert.True(_sut.IsEditMode);
        Assert.Equal("Pepperstone", _sut.Broker);
        Assert.Equal("ACC123",      _sut.AccountNumber);
        Assert.Equal("10000.00",    _sut.InitialCapitalText);
        Assert.Equal("USD",         _sut.BaseCurrency);
        Assert.Equal("1:200",       _sut.Leverage);
        Assert.Equal(new DateTime(2025, 1, 1), _sut.StartDate);
    }

    [Fact]
    public async Task InitializeAsync_WhenAccountExists_SetsSelectedAccountType()
    {
        _session.Setup(s => s.CurrentUser).Returns(SampleUser);
        _accountSvc.Setup(s => s.GetByUserIdAsync(1)).ReturnsAsync(SampleAccount);

        await _sut.InitializeAsync();

        Assert.NotNull(_sut.SelectedAccountType);
        Assert.Equal(AccountType.Real, _sut.SelectedAccountType!.Value);
    }

    [Fact]
    public async Task InitializeAsync_WhenNoUser_DoesNotThrow()
    {
        _session.Setup(s => s.CurrentUser).Returns((User?)null);

        var ex = await Record.ExceptionAsync(() => _sut.InitializeAsync());
        Assert.Null(ex);
    }

    // ── AccountTypes ─────────────────────────────────────────────────────

    [Fact]
    public void AccountTypes_ContainsThreeOptions()
    {
        Assert.Equal(3, _sut.AccountTypes.Count);
    }

    [Fact]
    public void AccountTypes_ContainsAllEnumValues()
    {
        var values = _sut.AccountTypes.Select(o => o.Value).ToList();
        Assert.Contains(AccountType.Demo, values);
        Assert.Contains(AccountType.Real, values);
        Assert.Contains(AccountType.Prop, values);
    }

    // ── ValidateCapital ──────────────────────────────────────────────────

    [Theory]
    [InlineData("100",      true)]
    [InlineData("10000.50", true)]
    [InlineData("0.01",     true)]
    [InlineData("0",        false)]
    [InlineData("-1",       false)]
    [InlineData("abc",      false)]
    public void ValidateCapital_ReturnsExpected(string input, bool isValid)
    {
        var ctx    = new System.ComponentModel.DataAnnotations.ValidationContext(new object());
        var result = TradingAccountViewModel.ValidateCapital(input, ctx);

        if (isValid)
            Assert.Equal(System.ComponentModel.DataAnnotations.ValidationResult.Success, result);
        else
            Assert.NotEqual(System.ComponentModel.DataAnnotations.ValidationResult.Success, result);
    }

    [Fact]
    public void ValidateCapital_EmptyString_ReturnsSuccess()
    {
        var ctx    = new System.ComponentModel.DataAnnotations.ValidationContext(new object());
        var result = TradingAccountViewModel.ValidateCapital(string.Empty, ctx);
        Assert.Equal(System.ComponentModel.DataAnnotations.ValidationResult.Success, result);
    }

    // ── SaveCommand — validaciones ───────────────────────────────────────

    [Fact]
    public async Task SaveCommand_EmptyBroker_HasErrors()
    {
        SetValidForm();
        _sut.Broker = string.Empty;

        await _sut.SaveCommand.ExecuteAsync(null);

        Assert.True(_sut.HasErrors);
        _accountSvc.Verify(s => s.CreateAsync(
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<AccountType>(), It.IsAny<decimal>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task SaveCommand_EmptyAccountNumber_HasErrors()
    {
        SetValidForm();
        _sut.AccountNumber = string.Empty;

        await _sut.SaveCommand.ExecuteAsync(null);

        Assert.True(_sut.HasErrors);
    }

    [Fact]
    public async Task SaveCommand_InvalidCapital_HasErrors()
    {
        SetValidForm();
        _sut.InitialCapitalText = "-100";

        await _sut.SaveCommand.ExecuteAsync(null);

        Assert.True(_sut.HasErrors);
    }

    [Fact]
    public async Task SaveCommand_InvalidLeverageFormat_HasErrors()
    {
        SetValidForm();
        _sut.Leverage = "100x";

        await _sut.SaveCommand.ExecuteAsync(null);

        Assert.True(_sut.HasErrors);
    }

    // ── SaveCommand — crear ──────────────────────────────────────────────

    [Fact]
    public async Task SaveCommand_NewAccount_CallsCreateAsync()
    {
        _session.Setup(s => s.CurrentUser).Returns(SampleUser);
        _accountSvc.Setup(s => s.GetByUserIdAsync(1)).ReturnsAsync((TradingAccount?)null);
        await _sut.InitializeAsync();
        SetValidForm();
        _accountSvc.Setup(s => s.CreateAsync(
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<AccountType>(), It.IsAny<decimal>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
            .ReturnsAsync(SampleAccount);

        await _sut.SaveCommand.ExecuteAsync(null);

        _accountSvc.Verify(s => s.CreateAsync(
            1, "Pepperstone", "ACC123", AccountType.Real, 10000m, "USD", "1:200",
            It.IsAny<DateTime>()), Times.Once);
    }

    [Fact]
    public async Task SaveCommand_OnCreateSuccess_SetsGeneralSuccess()
    {
        _session.Setup(s => s.CurrentUser).Returns(SampleUser);
        _accountSvc.Setup(s => s.GetByUserIdAsync(1)).ReturnsAsync((TradingAccount?)null);
        await _sut.InitializeAsync();
        SetValidForm();
        _accountSvc.Setup(s => s.CreateAsync(
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<AccountType>(), It.IsAny<decimal>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
            .ReturnsAsync(SampleAccount);

        await _sut.SaveCommand.ExecuteAsync(null);

        Assert.False(string.IsNullOrEmpty(_sut.GeneralSuccess));
    }

    [Fact]
    public async Task SaveCommand_OnCreateSuccess_SetsIsEditModeTrue()
    {
        _session.Setup(s => s.CurrentUser).Returns(SampleUser);
        _accountSvc.Setup(s => s.GetByUserIdAsync(1)).ReturnsAsync((TradingAccount?)null);
        await _sut.InitializeAsync();
        SetValidForm();
        _accountSvc.Setup(s => s.CreateAsync(
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<AccountType>(), It.IsAny<decimal>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
            .ReturnsAsync(SampleAccount);

        await _sut.SaveCommand.ExecuteAsync(null);

        Assert.True(_sut.IsEditMode);
    }

    // ── SaveCommand — editar ─────────────────────────────────────────────

    [Fact]
    public async Task SaveCommand_ExistingAccount_CallsUpdateAsync()
    {
        _session.Setup(s => s.CurrentUser).Returns(SampleUser);
        _accountSvc.Setup(s => s.GetByUserIdAsync(1)).ReturnsAsync(SampleAccount);
        await _sut.InitializeAsync();
        _accountSvc.Setup(s => s.UpdateAsync(
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<AccountType>(), It.IsAny<decimal>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
            .ReturnsAsync(SampleAccount);

        await _sut.SaveCommand.ExecuteAsync(null);

        _accountSvc.Verify(s => s.UpdateAsync(
            SampleAccount.Id, It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<AccountType>(), It.IsAny<decimal>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()), Times.Once);
    }

    [Fact]
    public async Task SaveCommand_OnUpdateSuccess_SetsGeneralSuccess()
    {
        _session.Setup(s => s.CurrentUser).Returns(SampleUser);
        _accountSvc.Setup(s => s.GetByUserIdAsync(1)).ReturnsAsync(SampleAccount);
        await _sut.InitializeAsync();
        _accountSvc.Setup(s => s.UpdateAsync(
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<AccountType>(), It.IsAny<decimal>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
            .ReturnsAsync(SampleAccount);

        await _sut.SaveCommand.ExecuteAsync(null);

        Assert.False(string.IsNullOrEmpty(_sut.GeneralSuccess));
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private void SetValidForm()
    {
        _sut.Broker             = "Pepperstone";
        _sut.AccountNumber      = "ACC123";
        _sut.SelectedAccountType = _sut.AccountTypes.First(o => o.Value == AccountType.Real);
        _sut.InitialCapitalText = "10000";
        _sut.BaseCurrency       = "USD";
        _sut.Leverage           = "1:200";
        _sut.StartDate          = DateTime.Today;
    }
}
