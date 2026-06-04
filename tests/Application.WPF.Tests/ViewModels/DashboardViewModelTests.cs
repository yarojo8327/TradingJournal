using Application.WPF.Services.Interfaces;
using Application.WPF.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Application.WPF.Tests.ViewModels;

public class DashboardViewModelTests
{
    private readonly Mock<IDialogService> _dialogMock = new();
    private readonly DashboardViewModel _sut;

    public DashboardViewModelTests()
    {
        _sut = new DashboardViewModel(_dialogMock.Object, NullLogger<DashboardViewModel>.Instance);
    }

    [Fact]
    public void Constructor_SetsTitleToDashboard()
    {
        Assert.Equal("Dashboard", _sut.Title);
    }

    [Fact]
    public async Task InitializeAsync_SetsStatusMessage()
    {
        await _sut.InitializeAsync();

        Assert.False(string.IsNullOrEmpty(_sut.StatusMessage));
    }

    [Fact]
    public void ShowSampleDialogCommand_CallsDialogService()
    {
        _sut.ShowSampleDialogCommand.Execute(null);

        _dialogMock.Verify(d => d.ShowInformation(
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task RefreshCommand_SetsIsBusyFalseWhenComplete()
    {
        await _sut.RefreshCommand.ExecuteAsync(null);

        Assert.False(_sut.IsBusy);
    }

    [Fact]
    public async Task RefreshCommand_UpdatesStatusMessage()
    {
        await _sut.InitializeAsync();
        var initial = _sut.StatusMessage;

        await Task.Delay(10);
        await _sut.RefreshCommand.ExecuteAsync(null);

        Assert.False(string.IsNullOrEmpty(_sut.StatusMessage));
    }
}
