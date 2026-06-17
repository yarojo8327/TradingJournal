using Application.WPF.Common.ViewModels;
using Application.WPF.Models.Entities;
using Application.WPF.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media.Imaging;

namespace Application.WPF.ViewModels.Playbook;

public partial class PlaybookViewModel : BaseViewModel
{
    private readonly IPlaybookService           _playbookService;
    private readonly ITradingStrategyService    _strategyService;
    private readonly ISessionService            _sessionService;
    private readonly ISymbolMappingService      _symbolMappingService;
    private readonly ILogger<PlaybookViewModel> _logger;

    private int _editingId;

    // ── Catálogos de filtros ──────────────────────────────────────────────

    public IReadOnlyList<string> FilterSymbolOptions =>
        new[] { string.Empty }.Concat(Symbols).ToList();

    public IReadOnlyList<int?> FilterRatingOptions { get; } =
        new List<int?> { null, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

    // ── Filtros activos ───────────────────────────────────────────────────

    [ObservableProperty] private string           _filterSymbol   = string.Empty;
    [ObservableProperty] private TradingStrategy? _filterStrategy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilterRatingDisplay))]
    private int? _filterRating;

    public string FilterRatingDisplay => FilterRating.HasValue ? $"★ {FilterRating}/10" : "Todas";

    public ObservableCollection<StarRatingItem> FilterStarItems { get; } =
        new(Enumerable.Range(1, 10).Select(i => new StarRatingItem { Value = i }));

    [RelayCommand]
    private void SetFilterRating(int value)
    {
        FilterRating = FilterRating == value ? null : value;
        RefreshFilterStars();
    }

    private void RefreshFilterStars()
    {
        foreach (var star in FilterStarItems)
            star.IsFilled = star.Value <= (FilterRating ?? 0);
    }

    // ── Visor flotante ────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsViewing))]
    private PlaybookEntry? _viewingEntry;

    public bool IsViewing => ViewingEntry is not null;

    [RelayCommand]
    private void ViewEntry(PlaybookEntry entry) => ViewingEntry = entry;

    [RelayCommand]
    private void CloseViewer() => ViewingEntry = null;

    // ── Catálogo de símbolos ──────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilterSymbolOptions))]
    private ObservableCollection<string> _symbols = new();

    // ── Paginación ────────────────────────────────────────────────────────

    private List<PlaybookEntry> _rawEntries = new();   // sin filtrar (todos del usuario)
    private List<PlaybookEntry> _allEntries = new();   // filtrados
    private const int PageSize = 6;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoEntries), nameof(PageInfo))]
    private int _totalCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageInfo), nameof(IsLastPage))]
    private int _totalPages = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageInfo), nameof(IsFirstPage), nameof(IsLastPage))]
    private int _currentPage = 1;

    public bool   IsFirstPage  => CurrentPage <= 1;
    public bool   IsLastPage   => CurrentPage >= TotalPages;
    public bool   HasNoEntries => TotalCount == 0;
    public string PageInfo     => $"{CurrentPage} / {TotalPages}  ·  {TotalCount} registros";

    [ObservableProperty] private ObservableCollection<PlaybookEntry> _pagedEntries = new();

    // ── Estado del formulario ─────────────────────────────────────────────

    [ObservableProperty] private bool   _isFormVisible;
    [ObservableProperty] private bool   _isEditMode;
    [ObservableProperty] private string _formTitle      = string.Empty;
    [ObservableProperty] private string _generalError   = string.Empty;
    [ObservableProperty] private string _generalSuccess = string.Empty;

    // ── Catálogos ─────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilterStrategies))]
    private ObservableCollection<TradingStrategy> _strategies = new();

    public IReadOnlyList<TradingStrategy?> FilterStrategies =>
        new[] { (TradingStrategy?)null }.Concat(Strategies).ToList();

    // ── Campos del formulario ─────────────────────────────────────────────

    [ObservableProperty] private string           _symbol = string.Empty;
    [ObservableProperty] private string           _notes  = string.Empty;
    [ObservableProperty] private TradingStrategy? _selectedStrategy;

    // Imagen
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasImage))]
    private byte[]? _imageData;

    [ObservableProperty] private string?      _imageMimeType;
    [ObservableProperty] private BitmapImage? _imagePreview;

    public bool HasImage => ImageData is { Length: > 0 };

    // Confluencias
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoConfluences), nameof(AverageRatingDisplay),
                               nameof(AverageValue), nameof(HasRatings), nameof(IsQualified),
                               nameof(QualityLabel))]
    private ObservableCollection<PlaybookConfluenceItem> _confluenceItems = new();

    // ── Rating de confluencias ────────────────────────────────────────────

    public bool   HasNoConfluences => !ConfluenceItems.Any();
    public bool   HasRatings       => ConfluenceItems.Any(c => c.IsRated);
    public bool   IsQualified      => AverageValue >= 6.5;
    public string QualityLabel     => IsQualified ? "✓ Setup calificado" : "✗ Setup no calificado";

    public double AverageValue
    {
        get
        {
            var rated = ConfluenceItems.Where(c => c.IsRated).ToList();
            return rated.Any() ? Math.Round(rated.Average(c => (double)c.Rating!.Value), 1) : 0;
        }
    }

    public string AverageRatingDisplay
    {
        get
        {
            var rated = ConfluenceItems.Where(c => c.IsRated).ToList();
            return rated.Any() ? $"{rated.Average(c => (double)c.Rating!.Value):F1} / 10" : "—";
        }
    }

    // ── Calificación manual (estrellas 1-10) ──────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ManualRatingDisplay))]
    private int? _manualRating;

    public string ManualRatingDisplay => ManualRating.HasValue ? $"{ManualRating}/10" : "—";

    public ObservableCollection<StarRatingItem> StarItems { get; } =
        new(Enumerable.Range(1, 10).Select(i => new StarRatingItem { Value = i }));

    [RelayCommand]
    private void SetManualRating(int value)
    {
        ManualRating = ManualRating == value ? null : value;
        RefreshStars();
    }

    private void RefreshStars()
    {
        foreach (var star in StarItems)
            star.IsFilled = star.Value <= (ManualRating ?? 0);
    }

    // ── Constructor ───────────────────────────────────────────────────────

    public PlaybookViewModel(
        IPlaybookService           playbookService,
        ITradingStrategyService    strategyService,
        ISessionService            sessionService,
        ISymbolMappingService      symbolMappingService,
        ILogger<PlaybookViewModel> logger)
    {
        _playbookService      = playbookService;
        _strategyService      = strategyService;
        _sessionService       = sessionService;
        _symbolMappingService = symbolMappingService;
        _logger               = logger;
        Title = "Playbook";
    }

    public override async Task InitializeAsync()
    {
        var user = _sessionService.CurrentUser;
        if (user is null) return;

        IsBusy = true;
        try
        {
            var strats = await _strategyService.GetAllByUserIdAsync(user.Id);
            Strategies = new ObservableCollection<TradingStrategy>(strats);

            await _symbolMappingService.EnsureDefaultsAsync();
            var names = await _symbolMappingService.GetCanonicalNamesAsync();
            Symbols = new ObservableCollection<string>(names);

            await LoadEntriesAsync(user.Id);
        }
        finally { IsBusy = false; }
    }

    // ── Paginación ────────────────────────────────────────────────────────

    private async Task LoadEntriesAsync(int userId)
    {
        var list    = await _playbookService.GetAllByUserIdAsync(userId);
        _rawEntries = list.ToList();
        _allEntries = ApplyFilters(_rawEntries).ToList();
        CurrentPage = 1;
        RefreshPage();
    }

    private IEnumerable<PlaybookEntry> ApplyFilters(IEnumerable<PlaybookEntry> source)
    {
        if (!string.IsNullOrWhiteSpace(FilterSymbol))
            source = source.Where(e => e.Symbol.Contains(FilterSymbol.Trim(), StringComparison.OrdinalIgnoreCase));
        if (FilterStrategy is not null)
            source = source.Where(e => e.StrategyId == FilterStrategy.Id);
        if (FilterRating.HasValue)
            source = source.Where(e => e.ManualRating == FilterRating.Value);
        return source;
    }

    private void RefreshPage()
    {
        TotalCount = _allEntries.Count;
        TotalPages = Math.Max(1, (int)Math.Ceiling((double)TotalCount / PageSize));
        if (CurrentPage > TotalPages) CurrentPage = TotalPages;
        if (CurrentPage < 1)         CurrentPage = 1;

        PagedEntries = new ObservableCollection<PlaybookEntry>(
            _allEntries.Skip((CurrentPage - 1) * PageSize).Take(PageSize));
    }

    [RelayCommand]
    private void NextPage() { if (CurrentPage < TotalPages) { CurrentPage++; RefreshPage(); } }

    [RelayCommand]
    private void PreviousPage() { if (CurrentPage > 1) { CurrentPage--; RefreshPage(); } }

    [RelayCommand]
    private void ApplyFilter()
    {
        _allEntries = ApplyFilters(_rawEntries).ToList();
        CurrentPage = 1;
        RefreshPage();
    }

    [RelayCommand]
    private void ClearFilter()
    {
        FilterSymbol   = string.Empty;
        FilterStrategy = null;
        FilterRating   = null;
        RefreshFilterStars();
        _allEntries    = _rawEntries.ToList();
        CurrentPage    = 1;
        RefreshPage();
    }

    // ── Imagen ────────────────────────────────────────────────────────────

    /// <summary>Llamado desde el code-behind tras el OpenFileDialog.</summary>
    public void SetImage(byte[] data, string mimeType)
    {
        ImageData     = data;
        ImageMimeType = mimeType;
        ImagePreview  = LoadBitmap(data);
    }

    [RelayCommand]
    private void RemoveImage()
    {
        ImageData     = null;
        ImageMimeType = null;
        ImagePreview  = null;
    }

    public static BitmapImage? LoadBitmap(byte[]? data)
    {
        if (data is not { Length: > 0 }) return null;
        try
        {
            var bmp = new BitmapImage();
            using var ms = new MemoryStream(data);
            bmp.BeginInit();
            bmp.CacheOption  = BitmapCacheOption.OnLoad;
            bmp.StreamSource = ms;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch { return null; }
    }

    // ── Cambio de estrategia → carga confluencias ─────────────────────────

    partial void OnSelectedStrategyChanged(TradingStrategy? value) => LoadConfluences(value);

    private void LoadConfluences(TradingStrategy? strategy)
    {
        DetachConfluenceHandlers();

        if (strategy is null || !strategy.Confluences.Any())
        {
            ConfluenceItems = new ObservableCollection<PlaybookConfluenceItem>();
            RefreshRatingProps();
            return;
        }

        var items = strategy.Confluences
            .OrderBy(c => c.OrderIndex)
            .Select(c => new PlaybookConfluenceItem
            {
                ConfluenceId   = c.Id,
                ConfluenceName = c.Name,
                OrderIndex     = c.OrderIndex,
                Rating         = null
            })
            .ToList();

        ConfluenceItems = new ObservableCollection<PlaybookConfluenceItem>(items);
        AttachConfluenceHandlers();
        RefreshRatingProps();
    }

    private void AttachConfluenceHandlers()
    {
        foreach (var item in ConfluenceItems)
            item.PropertyChanged += (_, _) => RefreshRatingProps();
    }

    private void DetachConfluenceHandlers()
    {
        foreach (var item in ConfluenceItems)
            item.PropertyChanged -= (_, _) => RefreshRatingProps();
    }

    private void RefreshRatingProps()
    {
        OnPropertyChanged(nameof(AverageValue));
        OnPropertyChanged(nameof(AverageRatingDisplay));
        OnPropertyChanged(nameof(HasRatings));
        OnPropertyChanged(nameof(IsQualified));
        OnPropertyChanged(nameof(QualityLabel));
        OnPropertyChanged(nameof(HasNoConfluences));
    }

    // ── Formulario ────────────────────────────────────────────────────────

    [RelayCommand]
    private void NewEntry()
    {
        ClearForm();
        _editingId    = 0;
        IsEditMode    = false;
        FormTitle     = "Nuevo registro de Playbook";
        IsFormVisible = true;
        GeneralError  = GeneralSuccess = string.Empty;
    }

    [RelayCommand]
    private void EditEntry(PlaybookEntry entry)
    {
        ClearForm();
        _editingId       = entry.Id;
        IsEditMode       = true;
        FormTitle        = "Editar registro de Playbook";
        GeneralError     = GeneralSuccess = string.Empty;

        Symbol           = entry.Symbol;
        Notes            = entry.Notes ?? string.Empty;
        ManualRating     = entry.ManualRating;
        RefreshStars();

        if (entry.HasImage)
        {
            ImageData     = entry.ImageData;
            ImageMimeType = entry.ImageMimeType;
            ImagePreview  = LoadBitmap(entry.ImageData);
        }

        SelectedStrategy = Strategies.FirstOrDefault(s => s.Id == entry.StrategyId);

        if (SelectedStrategy is not null && entry.ConfluenceRatings.Any())
        {
            foreach (var item in ConfluenceItems)
            {
                var saved = entry.ConfluenceRatings
                    .FirstOrDefault(r => r.ConfluenceId == item.ConfluenceId);
                if (saved is not null)
                    item.Rating = saved.Rating;
            }
            RefreshRatingProps();
        }

        IsFormVisible = true;
    }

    [RelayCommand]
    private void Cancel()
    {
        IsFormVisible = false;
        ClearForm();
        GeneralError = GeneralSuccess = string.Empty;
    }

    [RelayCommand]
    private async Task DeleteEntryAsync(PlaybookEntry entry)
    {
        try
        {
            await _playbookService.DeleteAsync(entry.Id);
            var user = _sessionService.CurrentUser;
            if (user is not null) await LoadEntriesAsync(user.Id);
            GeneralSuccess = "Registro eliminado.";
            GeneralError   = string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting playbook entry {Id}", entry.Id);
            GeneralError   = "Error al eliminar el registro.";
            GeneralSuccess = string.Empty;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        GeneralError = GeneralSuccess = string.Empty;

        if (string.IsNullOrWhiteSpace(Symbol))
        {
            GeneralError = "El símbolo es obligatorio.";
            return;
        }

        var user = _sessionService.CurrentUser;
        if (user is null) return;

        var ratedConfluences = ConfluenceItems
            .Where(c => c.IsRated)
            .Select(c => new PlaybookConfluenceRatingData(
                c.ConfluenceId, c.ConfluenceName, c.OrderIndex, c.Rating!.Value))
            .ToList();

        var avgRating = ratedConfluences.Any()
            ? (double?)ratedConfluences.Average(r => r.Rating)
            : null;

        var data = new PlaybookEntryData(
            UserId:            user.Id,
            StrategyId:        SelectedStrategy?.Id,
            Symbol:            Symbol.Trim(),
            Notes:             string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
            ImageData:         ImageData,
            ImageMimeType:     ImageMimeType,
            Rating:            avgRating,
            ManualRating:      ManualRating,
            ConfluenceRatings: ratedConfluences);

        IsBusy = true;
        try
        {
            if (IsEditMode)
            {
                await _playbookService.UpdateAsync(_editingId, data);
                GeneralSuccess = "Registro actualizado correctamente.";
            }
            else
            {
                await _playbookService.CreateAsync(data);
                GeneralSuccess = "Registro guardado correctamente.";
            }

            await LoadEntriesAsync(user.Id);
            IsFormVisible = false;
            ClearForm();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving playbook entry");
            GeneralError = "Error al guardar el registro.";
        }
        finally { IsBusy = false; }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void ClearForm()
    {
        Symbol           = string.Empty;
        Notes            = string.Empty;
        ManualRating     = null;
        RefreshStars();
        ImageData        = null;
        ImageMimeType    = null;
        ImagePreview     = null;
        SelectedStrategy = null;
        DetachConfluenceHandlers();
        ConfluenceItems  = new ObservableCollection<PlaybookConfluenceItem>();
        RefreshRatingProps();
    }
}
