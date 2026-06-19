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
    private readonly Mock<ITradingAccountService> _accountSvc  = new();
    private readonly Mock<ISessionService>        _session     = new();
    private readonly Mock<IDialogService>         _dialogSvc   = new();
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
            _dialogSvc.Object,
            NullLogger<TradingAccountViewModel>.Instance);
    }

    // ── InitializeAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task InitializeAsync_WhenNoAccounts_ShowsForm()
    {
        _session.Setup(s => s.CurrentUser).Returns(SampleUser);
        _accountSvc.Setup(s => s.GetAllByUserIdAsync(1)).ReturnsAsync(new List<TradingAccount>());

        await _sut.InitializeAsync();

        Assert.True(_sut.IsFormVisible);
        Assert.True(_sut.HasNoAccounts);
    }

    [Fact]
    public async Task InitializeAsync_WhenAccountsExist_LoadsList()
    {
        _session.Setup(s => s.CurrentUser).Returns(SampleUser);
        _accountSvc.Setup(s => s.GetAllByUserIdAsync(1))
                   .ReturnsAsync(new List<TradingAccount> { SampleAccount });

        await _sut.InitializeAsync();

        Assert.Single(_sut.Accounts);
        Assert.False(_sut.IsFormVisible);
        Assert.False(_sut.HasNoAccounts);
    }

    [Fact]
    public async Task InitializeAsync_WhenNoUser_DoesNotThrow()
    {
        _session.Setup(s => s.CurrentUser).Returns((User?)null);
        var ex = await Record.ExceptionAsync(() => _sut.InitializeAsync());
        Assert.Null(ex);
    }

    // ── NewAccountCommand ────────────────────────────────────────────────

    [Fact]
    public void NewAccountCommand_ShowsFormAndClearsFields()
    {
        _sut.Broker = "OldBroker";

        _sut.NewAccountCommand.Execute(null);

        Assert.True(_sut.IsFormVisible);
        Assert.False(_sut.IsEditMode);
        Assert.Empty(_sut.Broker);
    }

    // ── EditAccountCommand ───────────────────────────────────────────────

    [Fact]
    public void EditAccountCommand_LoadsAccountIntoForm()
    {
        _sut.EditAccountCommand.Execute(SampleAccount);

        Assert.True(_sut.IsFormVisible);
        Assert.True(_sut.IsEditMode);
        Assert.Equal("Pepperstone", _sut.Broker);
        Assert.Equal("ACC123",      _sut.AccountNumber);
        Assert.Equal("10000.00",    _sut.InitialCapitalText);
        Assert.Equal("USD",         _sut.BaseCurrency);
        Assert.Equal("1:200",       _sut.Leverage);
    }

    [Fact]
    public void EditAccountCommand_SetsSelectedAccountType()
    {
        _sut.EditAccountCommand.Execute(SampleAccount);

        Assert.NotNull(_sut.SelectedAccountType);
        Assert.Equal(AccountType.Real, _sut.SelectedAccountType!.Value);
    }

    // ── CancelCommand ────────────────────────────────────────────────────

    [Fact]
    public void CancelCommand_HidesForm()
    {
        _sut.NewAccountCommand.Execute(null);
        _sut.CancelCommand.Execute(null);

        Assert.False(_sut.IsFormVisible);
    }

    [Fact]
    public void CancelCommand_ClearsFormFields()
    {
        _sut.EditAccountCommand.Execute(SampleAccount);
        _sut.CancelCommand.Execute(null);

        Assert.Empty(_sut.Broker);
        Assert.Empty(_sut.AccountNumber);
    }

    // ── AccountTypes / Currencies / LeverageOptions ──────────────────────

    [Fact]
    public void AccountTypes_ContainsThreeOptions()
    {
        Assert.Equal(3, _sut.AccountTypes.Count);
    }

    [Fact]
    public void Currencies_ContainsCommonCurrencies()
    {
        Assert.Contains("USD", _sut.Currencies);
        Assert.Contains("EUR", _sut.Currencies);
        Assert.Contains("COP", _sut.Currencies);
        Assert.True(_sut.Currencies.Count >= 10);
    }

    [Fact]
    public void LeverageOptions_ContainsCommonValues()
    {
        Assert.Contains("1:100", _sut.LeverageOptions);
        Assert.Contains("1:500", _sut.LeverageOptions);
        Assert.True(_sut.LeverageOptions.Count >= 5);
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
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<bool>(), It.IsAny<decimal>()), Times.Never);
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
    public async Task SaveCommand_InvalidLeverage_HasErrors()
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
        _accountSvc.Setup(s => s.GetAllByUserIdAsync(1))
                   .ReturnsAsync(new List<TradingAccount> { SampleAccount });
        SetValidForm();
        _accountSvc.Setup(s => s.CreateAsync(
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<AccountType>(), It.IsAny<decimal>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<bool>(), It.IsAny<decimal>()))
            .ReturnsAsync(SampleAccount);

        await _sut.SaveCommand.ExecuteAsync(null);

        _accountSvc.Verify(s => s.CreateAsync(
            1, "Pepperstone", "ACC123", AccountType.Real, 10000m,
            "USD", "1:200", It.IsAny<DateTime>(), It.IsAny<bool>(), It.IsAny<decimal>()), Times.Once);
    }

    [Fact]
    public async Task SaveCommand_OnSuccess_HidesForm()
    {
        _session.Setup(s => s.CurrentUser).Returns(SampleUser);
        _accountSvc.Setup(s => s.GetAllByUserIdAsync(1))
                   .ReturnsAsync(new List<TradingAccount> { SampleAccount });
        SetValidForm();
        _accountSvc.Setup(s => s.CreateAsync(
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<AccountType>(), It.IsAny<decimal>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<bool>(), It.IsAny<decimal>()))
            .ReturnsAsync(SampleAccount);

        await _sut.SaveCommand.ExecuteAsync(null);

        Assert.False(_sut.IsFormVisible);
    }

    [Fact]
    public async Task SaveCommand_OnSuccess_SetsGeneralSuccess()
    {
        _session.Setup(s => s.CurrentUser).Returns(SampleUser);
        _accountSvc.Setup(s => s.GetAllByUserIdAsync(1))
                   .ReturnsAsync(new List<TradingAccount> { SampleAccount });
        SetValidForm();
        _accountSvc.Setup(s => s.CreateAsync(
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<AccountType>(), It.IsAny<decimal>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<bool>(), It.IsAny<decimal>()))
            .ReturnsAsync(SampleAccount);

        await _sut.SaveCommand.ExecuteAsync(null);

        Assert.False(string.IsNullOrEmpty(_sut.GeneralSuccess));
    }

    // ── SaveCommand — editar ─────────────────────────────────────────────

    [Fact]
    public async Task SaveCommand_ExistingAccount_CallsUpdateAsync()
    {
        _session.Setup(s => s.CurrentUser).Returns(SampleUser);
        _accountSvc.Setup(s => s.GetAllByUserIdAsync(1))
                   .ReturnsAsync(new List<TradingAccount> { SampleAccount });
        _sut.EditAccountCommand.Execute(SampleAccount);
        _accountSvc.Setup(s => s.UpdateAsync(
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<AccountType>(), It.IsAny<decimal>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<bool>(), It.IsAny<decimal>()))
            .ReturnsAsync(SampleAccount);

        await _sut.SaveCommand.ExecuteAsync(null);

        _accountSvc.Verify(s => s.UpdateAsync(
            SampleAccount.Id, It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<AccountType>(), It.IsAny<decimal>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<bool>(), It.IsAny<decimal>()), Times.Once);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private void SetValidForm()
    {
        _session.Setup(s => s.CurrentUser).Returns(SampleUser);
        _sut.IsFormVisible       = true;
        _sut.IsEditMode          = false;
        _sut.Broker              = "Pepperstone";
        _sut.AccountNumber       = "ACC123";
        _sut.SelectedAccountType = _sut.AccountTypes.First(o => o.Value == AccountType.Real);
        _sut.InitialCapitalText  = "10000";
        _sut.BaseCurrency        = "USD";
        _sut.Leverage            = "1:200";
        _sut.StartDate           = DateTime.Today;
    }

    // ── DeleteAccountCommand ─────────────────────────────────────────────

    [Fact]
    public async Task DeleteAccount_WhenHasTrades_SetsGeneralError()
    {
        _accountSvc.Setup(s => s.HasTradesAsync(SampleAccount.Id)).ReturnsAsync(true);

        await _sut.DeleteAccountCommand.ExecuteAsync(SampleAccount);

        Assert.False(string.IsNullOrEmpty(_sut.GeneralError));
        _accountSvc.Verify(s => s.DeleteAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAccount_WhenUserCancels_DoesNotDelete()
    {
        _accountSvc.Setup(s => s.HasTradesAsync(SampleAccount.Id)).ReturnsAsync(false);
        _dialogSvc.Setup(d => d.ShowConfirmation(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        await _sut.DeleteAccountCommand.ExecuteAsync(SampleAccount);

        _accountSvc.Verify(s => s.DeleteAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAccount_WhenConfirmed_DeletesAndReloads()
    {
        _session.Setup(s => s.CurrentUser).Returns(SampleUser);
        _accountSvc.Setup(s => s.HasTradesAsync(SampleAccount.Id)).ReturnsAsync(false);
        _dialogSvc.Setup(d => d.ShowConfirmation(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        _accountSvc.Setup(s => s.DeleteAsync(SampleAccount.Id)).Returns(Task.CompletedTask);
        _accountSvc.Setup(s => s.GetAllByUserIdAsync(1)).ReturnsAsync(new List<TradingAccount>());

        await _sut.DeleteAccountCommand.ExecuteAsync(SampleAccount);

        _accountSvc.Verify(s => s.DeleteAsync(SampleAccount.Id), Times.Once);
        Assert.False(string.IsNullOrEmpty(_sut.GeneralSuccess));
        Assert.Empty(_sut.GeneralError);
    }
}
