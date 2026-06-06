using Application.WPF.Models.Entities;
using Application.WPF.Services.Interfaces;
using Application.WPF.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Application.WPF.Tests.ViewModels;

public class DashboardViewModelTests
{
    private readonly Mock<ITradeService>           _tradeMock    = new();
    private readonly Mock<ITradingAccountService>  _accountMock  = new();
    private readonly Mock<ITradingStrategyService> _strategyMock = new();
    private readonly Mock<IPlaybookService>        _playbookMock = new();
    private readonly Mock<ISessionService>         _sessionMock  = new();

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
}
