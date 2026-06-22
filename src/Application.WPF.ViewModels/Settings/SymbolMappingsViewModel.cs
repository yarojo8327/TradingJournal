using Application.WPF.Common.ViewModels;
using Application.WPF.Models.Entities;
using Application.WPF.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace Application.WPF.ViewModels.Settings;

public partial class SymbolMappingsViewModel : BaseViewModel
{
    private readonly ISymbolMappingService _service;

    [ObservableProperty] private ObservableCollection<SymbolMapping> _items = new();
    [ObservableProperty] private ObservableCollection<SymbolMapping> _filteredItems = new();
    [ObservableProperty] private string _searchText = string.Empty;

    [ObservableProperty] private string  _newBrokerSymbol  = string.Empty;
    [ObservableProperty] private string  _newCanonicalName = string.Empty;
    [ObservableProperty] private string  _newCategory      = SymbolCategory.Forex;
    [ObservableProperty] private string  _newValuePerPoint = string.Empty;
    [ObservableProperty] private bool    _isEditing;
    [ObservableProperty] private SymbolMapping? _editingItem;
    [ObservableProperty] private string  _errorMessage = string.Empty;

    public IReadOnlyList<string> Categories => new[]
    {
        SymbolCategory.Forex, SymbolCategory.Index, SymbolCategory.Commodity,
        SymbolCategory.Crypto, SymbolCategory.Other
    };

    public SymbolMappingsViewModel(ISymbolMappingService service)
    {
        _service = service;
        Title = "Símbolos";
    }

    public override async Task InitializeAsync()
    {
        await _service.EnsureDefaultsAsync();
        await ReloadAsync();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private async Task ReloadAsync()
    {
        IsBusy = true;
        try
        {
            var all = await _service.GetAllAsync();
            Items = new ObservableCollection<SymbolMapping>(all);
            ApplyFilter();
        }
        finally { IsBusy = false; }
    }

    private void ApplyFilter()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            FilteredItems = new ObservableCollection<SymbolMapping>(Items);
            return;
        }
        var q = SearchText.Trim();
        FilteredItems = new ObservableCollection<SymbolMapping>(
            Items.Where(i =>
                i.BrokerSymbol.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                i.CanonicalName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                i.Category.Contains(q, StringComparison.OrdinalIgnoreCase)));
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(NewBrokerSymbol) || string.IsNullOrWhiteSpace(NewCanonicalName))
        {
            ErrorMessage = "Símbolo broker y nombre canónico son requeridos.";
            return;
        }

        decimal? valuePerPoint = decimal.TryParse(NewValuePerPoint,
            System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var vpp)
            ? vpp : null;

        IsBusy = true;
        try
        {
            if (IsEditing && EditingItem is not null)
            {
                await _service.UpdateAsync(EditingItem.Id, NewBrokerSymbol, NewCanonicalName, NewCategory, valuePerPoint);
            }
            else
            {
                await _service.CreateAsync(NewBrokerSymbol, NewCanonicalName, NewCategory, valuePerPoint);
            }
            CancelEdit();
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void StartEdit(SymbolMapping item)
    {
        EditingItem      = item;
        NewBrokerSymbol  = item.BrokerSymbol;
        NewCanonicalName = item.CanonicalName;
        NewCategory      = item.Category;
        NewValuePerPoint = item.ValuePerPoint?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        IsEditing        = true;
        ErrorMessage     = string.Empty;
    }

    [RelayCommand]
    private void CancelEdit()
    {
        EditingItem      = null;
        NewBrokerSymbol  = string.Empty;
        NewCanonicalName = string.Empty;
        NewCategory      = SymbolCategory.Forex;
        NewValuePerPoint = string.Empty;
        IsEditing        = false;
        ErrorMessage     = string.Empty;
    }

    [RelayCommand]
    private async Task DeleteAsync(SymbolMapping item)
    {
        IsBusy = true;
        try
        {
            await _service.DeleteAsync(item.Id);
            await ReloadAsync();
        }
        finally { IsBusy = false; }
    }
}
