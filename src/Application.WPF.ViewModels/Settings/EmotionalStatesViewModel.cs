using Application.WPF.Common.ViewModels;
using Application.WPF.Models.Entities;
using Application.WPF.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace Application.WPF.ViewModels.Settings;

public partial class EmotionalStatesViewModel : BaseViewModel
{
    private readonly IJournalListService _service;
    private readonly ISessionService     _session;

    [ObservableProperty] private ObservableCollection<JournalListItem> _items = new();
    [ObservableProperty] private string _newName    = string.Empty;
    [ObservableProperty] private string _errorText  = string.Empty;

    public EmotionalStatesViewModel(IJournalListService service, ISessionService session)
    {
        _service = service;
        _session = session;
        Title    = "Estados emocionales";
    }

    public override async Task InitializeAsync()
    {
        var user = _session.CurrentUser;
        if (user is null) return;
        await _service.EnsureDefaultsAsync(user.Id);
        await LoadAsync(user.Id);
    }

    private async Task LoadAsync(int userId)
    {
        var list = await _service.GetItemsAsync(userId, JournalListCategory.EmotionalState);
        Items = new ObservableCollection<JournalListItem>(list);
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        var name = NewName.Trim();
        if (string.IsNullOrWhiteSpace(name)) return;
        var user = _session.CurrentUser;
        if (user is null) return;
        var item = await _service.CreateAsync(user.Id, JournalListCategory.EmotionalState, name);
        Items.Add(item);
        NewName   = string.Empty;
        ErrorText = string.Empty;
    }

    [RelayCommand]
    private async Task DeleteAsync(JournalListItem item)
    {
        await _service.DeleteAsync(item.Id);
        Items.Remove(item);
    }
}
