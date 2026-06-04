using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;

namespace Application.WPF.Common.ViewModels;

public abstract partial class BaseViewModel : ObservableValidator
{
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _title = string.Empty;

    protected void ValidateAll() => ValidateAllProperties();

    public virtual Task InitializeAsync() => Task.CompletedTask;

    public virtual void Dispose() { }
}
