using Application.WPF.Common.ViewModels;
using Application.WPF.Models.Entities;
using Application.WPF.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace Application.WPF.ViewModels.Settings;

public partial class JournalSettingsViewModel : BaseViewModel
{
    private readonly IJournalListService _service;
    private readonly ISessionService     _session;

    [ObservableProperty] private ObservableCollection<JournalListItem> _emotionalStates = new();
    [ObservableProperty] private ObservableCollection<JournalListItem> _mistakeTypes    = new();
    [ObservableProperty] private string _newEmotionalState = string.Empty;
    [ObservableProperty] private string _newMistakeType    = string.Empty;
    [ObservableProperty] private string _generalError      = string.Empty;

    public JournalSettingsViewModel(IJournalListService service, ISessionService session)
    {
        _service = service;
        _session = session;
        Title    = "Configuración — Bitácora";
    }

    public override async Task InitializeAsync()
    {
        var user = _session.CurrentUser;
        if (user is null) return;
        await LoadAsync(user.Id);
    }

    private async Task LoadAsync(int userId)
    {
        var emotional = await _service.GetItemsAsync(userId, JournalListCategory.EmotionalState);
        EmotionalStates = new ObservableCollection<JournalListItem>(emotional);

        var mistakes = await _service.GetItemsAsync(userId, JournalListCategory.MistakeType);
        MistakeTypes = new ObservableCollection<JournalListItem>(mistakes);
    }

    [RelayCommand]
    private async Task AddEmotionalStateAsync()
    {
        var name = NewEmotionalState.Trim();
        if (string.IsNullOrWhiteSpace(name)) return;
        var user = _session.CurrentUser;
        if (user is null) return;

        var item = await _service.CreateAsync(user.Id, JournalListCategory.EmotionalState, name);
        EmotionalStates.Add(item);
        NewEmotionalState = string.Empty;
        GeneralError = string.Empty;
    }

    [RelayCommand]
    private async Task DeleteEmotionalStateAsync(JournalListItem item)
    {
        await _service.DeleteAsync(item.Id);
        EmotionalStates.Remove(item);
    }

    [RelayCommand]
    private async Task AddMistakeTypeAsync()
    {
        var name = NewMistakeType.Trim();
        if (string.IsNullOrWhiteSpace(name)) return;
        var user = _session.CurrentUser;
        if (user is null) return;

        var item = await _service.CreateAsync(user.Id, JournalListCategory.MistakeType, name);
        MistakeTypes.Add(item);
        NewMistakeType = string.Empty;
        GeneralError   = string.Empty;
    }

    [RelayCommand]
    private async Task DeleteMistakeTypeAsync(JournalListItem item)
    {
        await _service.DeleteAsync(item.Id);
        MistakeTypes.Remove(item);
    }
}
