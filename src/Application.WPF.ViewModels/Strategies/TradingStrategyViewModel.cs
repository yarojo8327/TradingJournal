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
    private readonly ITradingStrategyService          _strategyService;
    private readonly ISessionService                  _sessionService;
    private readonly IDialogService                   _dialogService;
    private readonly ILogger<TradingStrategyViewModel> _logger;

    private int _strategyId;

    // ── Lista ─────────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoStrategies))]
    private ObservableCollection<StrategyEntity> _strategies = new();

    public bool HasNoStrategies => !Strategies.Any();

    // ── Estado del formulario ─────────────────────────────────────────────

    [ObservableProperty] private bool   _isFormVisible;
    [ObservableProperty] private bool   _isEditMode;
    [ObservableProperty] private string _formTitle    = "Nueva estrategia";
    [ObservableProperty] private string _generalError  = string.Empty;
    [ObservableProperty] private string _generalSuccess = string.Empty;

    // ── Campos del formulario ─────────────────────────────────────────────

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "El título de la estrategia es requerido.")]
    [MaxLength(200, ErrorMessage = "No puede superar 200 caracteres.")]
    private string _title = string.Empty;

    [ObservableProperty] private string _description = string.Empty;

    // ── Reglas ────────────────────────────────────────────────────────────

    [ObservableProperty] private ObservableCollection<StrategyRuleItem> _rules = new();
    [ObservableProperty] private string _newRuleText = string.Empty;

    // ── Imagen ────────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasImage))]
    private byte[]? _imageData;

    [ObservableProperty] private string?       _imageMimeType;
    [ObservableProperty] private BitmapImage?  _imagePreview;

    public bool HasImage => ImageData != null && ImageData.Length > 0;

    public TradingStrategyViewModel(
        ITradingStrategyService           strategyService,
        ISessionService                   sessionService,
        IDialogService                    dialogService,
        ILogger<TradingStrategyViewModel> logger)
    {
        _strategyService = strategyService;
        _sessionService  = sessionService;
        _dialogService   = dialogService;
        _logger          = logger;
        Title            = "Estrategias de Trading";
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

    // ── Comandos de lista ─────────────────────────────────────────────────

    [RelayCommand]
    private void NewStrategy()
    {
        ClearForm();
        IsEditMode    = false;
        IsFormVisible = true;
        FormTitle     = "Nueva estrategia";
        GeneralError  = GeneralSuccess = string.Empty;
    }

    [RelayCommand]
    private void EditStrategy(StrategyEntity strategy)
    {
        _strategyId   = strategy.Id;
        Title         = strategy.Title;
        Description   = strategy.Description ?? string.Empty;
        IsEditMode    = true;
        IsFormVisible = true;
        FormTitle     = "Editar estrategia";
        GeneralError  = GeneralSuccess = string.Empty;

        Rules = new ObservableCollection<StrategyRuleItem>(
            strategy.Rules.OrderBy(r => r.OrderIndex).Select((r, i) => new StrategyRuleItem
            {
                ExistingId  = r.Id,
                Description = r.Description,
                OrderIndex  = i
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
        IsFormVisible  = false;
        GeneralError   = GeneralSuccess = string.Empty;
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
        ValidateProperty(Title, nameof(Title));
        if (HasErrors) return;

        var user = _sessionService.CurrentUser;
        if (user is null) return;

        // Materializar antes de entrar al bloque async para evitar iterar
        // la colección después de que ClearForm() la haya vaciado.
        var ruleDescriptions = Rules.Select(r => r.Description).ToList();

        IsBusy = true;
        try
        {
            if (IsEditMode)
            {
                await _strategyService.UpdateAsync(
                    _strategyId, Title, Description,
                    ImageData, ImageMimeType, ruleDescriptions);
                GeneralSuccess = "Estrategia actualizada correctamente.";
            }
            else
            {
                await _strategyService.CreateAsync(
                    user.Id, Title, Description,
                    ImageData, ImageMimeType, ruleDescriptions);
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
        _strategyId  = 0;
        Title        = string.Empty;
        Description  = string.Empty;
        NewRuleText  = string.Empty;
        Rules        = new ObservableCollection<StrategyRuleItem>();
        SetImage(null, null);
        ClearErrors();
    }
}
