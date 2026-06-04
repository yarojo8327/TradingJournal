using Application.WPF.Common.Localization;
using Application.WPF.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace Application.WPF.Services.Localization;

public sealed class LocalizationService : ILocalizationService
{
    private readonly ResourceManager _resourceManager;
    private readonly ILogger<LocalizationService> _logger;
    private CultureInfo _currentCultureInfo;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string CurrentCulture => _currentCultureInfo.Name;
    public IReadOnlyList<SupportedLanguage> AvailableLanguages => SupportedLanguage.All;

    public string this[string key] => Get(key);

    public LocalizationService(ILogger<LocalizationService> logger, string initialCulture = "en-US")
    {
        _logger = logger;
        _resourceManager = new ResourceManager(
            "Application.WPF.Common.Localization.Strings",
            typeof(SupportedLanguage).Assembly);

        _currentCultureInfo = CultureInfo.GetCultureInfo(initialCulture);
    }

    public string Get(string key)
    {
        try
        {
            return _resourceManager.GetString(key, _currentCultureInfo)
                   ?? $"[{key}]";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Missing localization key: {Key} for culture {Culture}", key, CurrentCulture);
            return $"[{key}]";
        }
    }

    public void ChangeLanguage(string cultureCode)
    {
        if (cultureCode == CurrentCulture) return;

        _logger.LogInformation("Changing language from {From} to {To}", CurrentCulture, cultureCode);

        _currentCultureInfo = CultureInfo.GetCultureInfo(cultureCode);
        Thread.CurrentThread.CurrentUICulture = _currentCultureInfo;
        Thread.CurrentThread.CurrentCulture   = _currentCultureInfo;

        // Notify all bindings — empty string refreshes every indexed property
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentCulture)));
    }
}
