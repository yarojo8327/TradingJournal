using Application.WPF.Common.ViewModels;
using Application.WPF.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media.Imaging;
using StrategyEntity = Application.WPF.Models.Entities.TradingStrategy;

namespace Application.WPF.ViewModels.Strategies;

public partial class TradingStrategyViewModel : BaseViewModel
{
    private readonly ITradingStrategyService           _strategyService;
    private readonly ISessionService                   _sessionService;
    private readonly IDialogService                    _dialogService;
    private readonly ILogger<TradingStrategyViewModel> _logger;
    private readonly Func<StrategyRaterViewModel>      _raterFactory;

    private int _strategyId;

    // ── Lista ─────────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoStrategies))]
    private ObservableCollection<StrategyEntity> _strategies = new();

    public bool HasNoStrategies => !Strategies.Any();

    // ── Estado del formulario ─────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowViewingPanel))]
    [NotifyPropertyChangedFor(nameof(ShowDetailsPlaceholder))]
    private bool   _isFormVisible;
    [ObservableProperty] private bool   _isEditMode;
    [ObservableProperty] private string _formTitle     = "Nueva estrategia";
    [ObservableProperty] private string _generalError  = string.Empty;
    [ObservableProperty] private string _generalSuccess = string.Empty;

    // ── Campos del formulario ─────────────────────────────────────────────

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "El título de la estrategia es requerido.")]
    [MaxLength(200, ErrorMessage = "No puede superar 200 caracteres.")]
    private string _strategyTitle = string.Empty;

    [ObservableProperty] private string _description = string.Empty;

    // ── Reglas ────────────────────────────────────────────────────────────

    [ObservableProperty] private ObservableCollection<StrategyRuleItem> _rules = new();
    [ObservableProperty] private string _newRuleText = string.Empty;

    // ── Confluencias ──────────────────────────────────────────────────────

    [ObservableProperty] private ObservableCollection<StrategyConfluenceItem> _confluences = new();
    [ObservableProperty] private string _newConfluenceText = string.Empty;

    // ── Imagen ────────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasImage))]
    private byte[]? _imageData;

    [ObservableProperty] private string?      _imageMimeType;
    [ObservableProperty] private BitmapImage? _imagePreview;

    public bool HasImage => ImageData != null && ImageData.Length > 0;

    // ── Panel de detalles (lectura) ─────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasViewingStrategy))]
    [NotifyPropertyChangedFor(nameof(ShowViewingPanel))]
    [NotifyPropertyChangedFor(nameof(ShowDetailsPlaceholder))]
    private StrategyEntity? _viewingStrategy;

    public bool HasViewingStrategy => ViewingStrategy is not null;

    /// <summary>El panel de detalles se muestra cuando hay una estrategia seleccionada y el formulario está cerrado.</summary>
    public bool ShowViewingPanel => !IsFormVisible && HasViewingStrategy;

    /// <summary>El placeholder se muestra cuando no hay nada seleccionado ni en edición.</summary>
    public bool ShowDetailsPlaceholder => !IsFormVisible && !HasViewingStrategy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ViewingHasImage))]
    [NotifyPropertyChangedFor(nameof(ViewingHasNoImage))]
    private BitmapImage? _viewingImage;

    public bool ViewingHasImage   => ViewingImage is not null;
    public bool ViewingHasNoImage => ViewingImage is null;

    public TradingStrategyViewModel(
        ITradingStrategyService           strategyService,
        ISessionService                   sessionService,
        IDialogService                    dialogService,
        ILogger<TradingStrategyViewModel> logger,
        Func<StrategyRaterViewModel>      raterFactory)
    {
        _strategyService = strategyService;
        _sessionService  = sessionService;
        _dialogService   = dialogService;
        _logger          = logger;
        _raterFactory    = raterFactory;
    }

    public StrategyRaterViewModel CreateRater(StrategyEntity strategy)
    {
        var vm = _raterFactory();
        vm.LoadStrategy(strategy);
        return vm;
    }

    public override async Task InitializeAsync()
    {
        var user = _sessionService.CurrentUser;
        if (user is null) return;
        await LoadStrategiesAsync(user.Id);
    }

    private async Task LoadStrategiesAsync(int userId)
    {
        var list = await _strategyService.GetAllByUserIdAsync(userId);
        Strategies = new ObservableCollection<StrategyEntity>(list);
    }

    public async Task RefreshAsync()
    {
        var user = _sessionService.CurrentUser;
        if (user is not null) await LoadStrategiesAsync(user.Id);
    }

    // ── Comandos de lista ─────────────────────────────────────────────────

    /// <summary>Selecciona una estrategia para mostrar sus detalles de solo lectura en el panel derecho.</summary>
    [RelayCommand]
    private void SelectStrategy(StrategyEntity strategy)
    {
        IsFormVisible   = false;
        ViewingStrategy = strategy;
        ViewingImage    = strategy.ImageData is { Length: > 0 }
            ? BytesToBitmap(strategy.ImageData)
            : null;
    }

    [RelayCommand]
    private void NewStrategy()
    {
        ClearForm();
        IsEditMode      = false;
        IsFormVisible   = true;
        ViewingStrategy = null;
        FormTitle       = "Nueva estrategia";
        GeneralError    = GeneralSuccess = string.Empty;
    }

    [RelayCommand]
    private void EditStrategy(StrategyEntity strategy)
    {
        _strategyId     = strategy.Id;
        StrategyTitle   = strategy.Title;
        Description     = strategy.Description ?? string.Empty;
        IsEditMode      = true;
        IsFormVisible   = true;
        ViewingStrategy = null;
        FormTitle       = "Editar estrategia";
        GeneralError    = GeneralSuccess = string.Empty;

        Rules = new ObservableCollection<StrategyRuleItem>(
            strategy.Rules.OrderBy(r => r.OrderIndex).Select((r, i) => new StrategyRuleItem
            {
                ExistingId  = r.Id,
                Description = r.Description,
                OrderIndex  = i
            }));

        Confluences = new ObservableCollection<StrategyConfluenceItem>(
            strategy.Confluences.OrderBy(c => c.OrderIndex).Select((c, i) => new StrategyConfluenceItem
            {
                ExistingId = c.Id,
                Name       = c.Name,
                OrderIndex = i
            }));

        SetImage(strategy.ImageData, strategy.ImageMimeType);
    }

    [RelayCommand]
    private async Task DeleteStrategyAsync(StrategyEntity strategy)
    {
        var confirmed = _dialogService.ShowConfirmation(
            $"¿Eliminar la estrategia «{strategy.Title}»? Esta acción no se puede deshacer.",
            "Eliminar estrategia");

        if (!confirmed) return;

        IsBusy = true;
        try
        {
            await _strategyService.DeleteAsync(strategy.Id);
            if (ViewingStrategy?.Id == strategy.Id)
            {
                ViewingStrategy = null;
                ViewingImage    = null;
            }
            var user = _sessionService.CurrentUser;
            if (user is not null) await LoadStrategiesAsync(user.Id);
            GeneralSuccess = "Estrategia eliminada correctamente.";
            GeneralError   = string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting strategy {Id}", strategy.Id);
            GeneralError = "Error al eliminar la estrategia.";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void Cancel()
    {
        ClearForm();
        IsFormVisible = false;
        GeneralError  = GeneralSuccess = string.Empty;
    }

    // ── Comandos de reglas ────────────────────────────────────────────────

    [RelayCommand]
    private void AddRule()
    {
        if (string.IsNullOrWhiteSpace(NewRuleText)) return;
        Rules.Add(new StrategyRuleItem
        {
            Description = NewRuleText.Trim(),
            OrderIndex  = Rules.Count
        });
        NewRuleText = string.Empty;
    }

    [RelayCommand]
    private void RemoveRule(StrategyRuleItem rule)
    {
        Rules.Remove(rule);
        for (int i = 0; i < Rules.Count; i++)
            Rules[i].OrderIndex = i;
    }

    // ── Comandos de confluencias ──────────────────────────────────────────

    [RelayCommand]
    private void AddConfluence()
    {
        if (string.IsNullOrWhiteSpace(NewConfluenceText)) return;
        Confluences.Add(new StrategyConfluenceItem
        {
            Name       = NewConfluenceText.Trim(),
            OrderIndex = Confluences.Count
        });
        NewConfluenceText = string.Empty;
    }

    [RelayCommand]
    private void RemoveConfluence(StrategyConfluenceItem confluence)
    {
        Confluences.Remove(confluence);
        for (int i = 0; i < Confluences.Count; i++)
            Confluences[i].OrderIndex = i;
    }

    // ── Imagen ────────────────────────────────────────────────────────────

    public void SetImage(byte[]? data, string? mimeType)
    {
        ImageData     = data;
        ImageMimeType = mimeType;
        ImagePreview  = data is { Length: > 0 } ? BytesToBitmap(data) : null;
    }

    [RelayCommand]
    private void ClearImage()
    {
        ImageData     = null;
        ImageMimeType = null;
        ImagePreview  = null;
    }

    private static BitmapImage? BytesToBitmap(byte[] data)
    {
        try
        {
            var img = new BitmapImage();
            using var ms = new System.IO.MemoryStream(data);
            img.BeginInit();
            img.CacheOption  = BitmapCacheOption.OnLoad;
            img.StreamSource = ms;
            img.EndInit();
            img.Freeze();
            return img;
        }
        catch { return null; }
    }

    // ── Guardar ───────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task SaveAsync()
    {
        GeneralError = GeneralSuccess = string.Empty;
        ValidateProperty(StrategyTitle, nameof(StrategyTitle));
        if (HasErrors) return;

        var user = _sessionService.CurrentUser;
        if (user is null) return;

        var ruleDescriptions     = Rules.Select(r => r.Description).ToList();
        var confluenceNames      = Confluences.Select(c => c.Name).ToList();

        IsBusy = true;
        try
        {
            if (IsEditMode)
            {
                await _strategyService.UpdateAsync(
                    _strategyId, StrategyTitle, Description,
                    ImageData, ImageMimeType, ruleDescriptions, confluenceNames);
                GeneralSuccess = "Estrategia actualizada correctamente.";
            }
            else
            {
                await _strategyService.CreateAsync(
                    user.Id, StrategyTitle, Description,
                    ImageData, ImageMimeType, ruleDescriptions, confluenceNames);
                GeneralSuccess = "Estrategia registrada correctamente.";
            }

            await LoadStrategiesAsync(user.Id);
            IsFormVisible = false;
            ClearForm();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving strategy");
            GeneralError = "Ocurrió un error al guardar la estrategia.";
        }
        finally { IsBusy = false; }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void ClearForm()
    {
        _strategyId       = 0;
        StrategyTitle     = string.Empty;
        Description       = string.Empty;
        NewRuleText       = string.Empty;
        NewConfluenceText = string.Empty;
        Rules             = new ObservableCollection<StrategyRuleItem>();
        Confluences       = new ObservableCollection<StrategyConfluenceItem>();
        SetImage(null, null);
        ClearErrors();
    }
}
