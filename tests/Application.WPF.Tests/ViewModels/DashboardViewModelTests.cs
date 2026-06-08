using Application.WPF.Models.Entities;
using Application.WPF.Services.Interfaces;
using Application.WPF.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Application.WPF.Tests.ViewModels;

public class DashboardViewModelTests
{
    private readonly Mock<ITradeService>           _tradeMock      = new();
    private readonly Mock<ITradingAccountService>  _accountMock    = new();
    private readonly Mock<ITradingStrategyService> _strategyMock   = new();
    private readonly Mock<IPlaybookService>        _playbookMock   = new();
    private readonly Mock<ISessionService>         _sessionMock    = new();
    private readonly Mock<INavigationService>      _navigationMock = new();

    private DashboardViewModel BuildSut()
    {
        // Default: authenticated user, empty data
        _sessionMock.Setup(s => s.CurrentUser).Returns(new User { Id = 1, Username = "test" });
        _tradeMock.Setup(s => s.GetAllByUserIdAsync(It.IsAny<int>()))
                  .ReturnsAsync(new List<TradeEntry>());
        _accountMock.Setup(s => s.GetAllByUserIdAsync(It.IsAny<int>()))
                    .ReturnsAsync(new List<TradingAccount>());
        _playbookMock.Setup(s => s.GetAllByUserIdAsync(It.IsAny<int>()))
                     .ReturnsAsync(new List<PlaybookEntry>());

        return new DashboardViewModel(
            _tradeMock.Object,
            _accountMock.Object,
            _strategyMock.Object,
            _playbookMock.Object,
            _sessionMock.Object,
            _navigationMock.Object,
            NullLogger<DashboardViewModel>.Instance);
    }

    [Fact]
    public void Constructor_SetsTitleToPanel()
    {
        var sut = BuildSut();
        Assert.Equal("Panel", sut.Title);
    }

    [Fact]
    public async Task InitializeAsync_LoadsDataWithoutError()
    {
        var sut = BuildSut();
        await sut.InitializeAsync();
        Assert.False(sut.IsBusy);
    }

    [Fact]
    public async Task InitializeAsync_SetsHasDataFalseWhenNoTrades()
    {
        var sut = BuildSut();
        await sut.InitializeAsync();
        Assert.False(sut.HasData);
    }

    [Fact]
    public async Task RefreshCommand_SetsIsBusyFalseWhenComplete()
    {
        var sut = BuildSut();
        await sut.RefreshCommand.ExecuteAsync(null);
        Assert.False(sut.IsBusy);
    }

    [Fact]
    public async Task RefreshCommand_SetsLastRefreshed()
    {
        var sut = BuildSut();
        await sut.RefreshCommand.ExecuteAsync(null);
        Assert.False(string.IsNullOrEmpty(sut.LastRefreshed));
    }

    [Fact]
    public async Task InitializeAsync_PopulatesAccountsForFilter_WithAllAccountsItem()
    {
        var sut = BuildSut();   // sets default empty mocks

        // Override to return 2 accounts
        var accounts = new List<TradingAccount>
        {
            new() { Id = 1, Broker = "Broker A", AccountNumber = "001", InitialCapital = 1000m, BaseCurrency = "USD", StartDate = DateTime.Today },
            new() { Id = 2, Broker = "Broker B", AccountNumber = "002", InitialCapital = 2000m, BaseCurrency = "USD", StartDate = DateTime.Today },
        };
        _accountMock.Setup(s => s.GetAllByUserIdAsync(It.IsAny<int>()))
                    .ReturnsAsync(accounts);

        await sut.InitializeAsync();

        // Expects 3 items: "Todas las cuentas" + 2 accounts
        Assert.Equal(3, sut.AccountsForFilter.Count);
        Assert.Null(sut.AccountsForFilter[0].Account);
        Assert.Equal("Todas las cuentas", sut.AccountsForFilter[0].DisplayName);
    }

    [Fact]
    public async Task AccountFilter_WhenAccountSelected_FiltersTradesCorrectly()
    {
        var sut = BuildSut();

        var trades = new List<TradeEntry>
        {
            new() { Id = 1, AccountId = 1, Symbol = "EURUSD", EntryDate = DateTime.Today,
                    EntryPrice = 1.1m, Direction = Application.WPF.Models.Enums.TradeDirection.Long,
                    Result = Application.WPF.Models.Enums.TradeResult.Profit, ProfitLoss = 100m },
            new() { Id = 2, AccountId = 2, Symbol = "GBPUSD", EntryDate = DateTime.Today,
                    EntryPrice = 1.2m, Direction = Application.WPF.Models.Enums.TradeDirection.Long,
                    Result = Application.WPF.Models.Enums.TradeResult.Loss,  ProfitLoss = -50m },
        };
        var accounts = new List<TradingAccount>
        {
            new() { Id = 1, Broker = "Broker A", AccountNumber = "001", InitialCapital = 1000m, BaseCurrency = "USD", StartDate = DateTime.Today },
            new() { Id = 2, Broker = "Broker B", AccountNumber = "002", InitialCapital = 2000m, BaseCurrency = "USD", StartDate = DateTime.Today },
        };
        _tradeMock.Setup(s => s.GetAllByUserIdAsync(It.IsAny<int>())).ReturnsAsync(trades);
        _accountMock.Setup(s => s.GetAllByUserIdAsync(It.IsAny<int>())).ReturnsAsync(accounts);

        await sut.InitializeAsync();

        // Default = first account (Id 1) → 1 trade (profit)
        Assert.Equal(1, sut.TotalTrades);
        Assert.Equal(1, sut.WinCount);
        Assert.Equal(0, sut.LossCount);

        // Switch to "Todas las cuentas" (null) → 2 trades
        sut.SelectedAccountFilter = sut.AccountsForFilter.First(a => a.Account is null);
        await Task.Delay(200); // allow async reload
        Assert.Equal(2, sut.TotalTrades);
    }
}
