using Application.WPF.Common.ViewModels;
using Xunit;

namespace Application.WPF.Tests.ViewModels;

public class BaseViewModelTests
{
    private readonly ConcreteViewModel _sut = new();

    [Fact]
    public void IsBusy_DefaultFalse()
    {
        Assert.False(_sut.IsBusy);
    }

    [Fact]
    public void IsBusy_RaisesPropertyChanged()
    {
        var raised = new List<string?>();
        _sut.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        _sut.IsBusy = true;

        Assert.Contains(nameof(_sut.IsBusy), raised);
    }

    [Fact]
    public void Title_DefaultEmpty()
    {
        Assert.Equal(string.Empty, _sut.Title);
    }

    [Fact]
    public void Title_CanBeSet()
    {
        _sut.Title = "Test";
        Assert.Equal("Test", _sut.Title);
    }

    [Fact]
    public async Task InitializeAsync_CompletesSuccessfully()
    {
        await _sut.InitializeAsync();
    }

    public class ConcreteViewModel : BaseViewModel
    {
        public new bool IsBusy
        {
            get => base.IsBusy;
            set => base.IsBusy = value;
        }

        public new string Title
        {
            get => base.Title;
            set => base.Title = value;
        }
    }
}
