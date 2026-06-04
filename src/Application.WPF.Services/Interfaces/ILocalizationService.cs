using Application.WPF.Common.Localization;
using System.ComponentModel;

namespace Application.WPF.Services.Interfaces;

public interface ILocalizationService : INotifyPropertyChanged
{
    string CurrentCulture { get; }
    IReadOnlyList<SupportedLanguage> AvailableLanguages { get; }
    string this[string key] { get; }
    void ChangeLanguage(string cultureCode);
    string Get(string key);
}
