namespace Application.WPF.Common.Localization;

public class SupportedLanguage
{
    public string CultureCode { get; init; } = string.Empty;
    public string DisplayName  { get; init; } = string.Empty;
    public string NativeName   { get; init; } = string.Empty;

    public static readonly SupportedLanguage EnUS = new()
    {
        CultureCode = "en-US",
        DisplayName = "English (US)",
        NativeName  = "English"
    };

    public static readonly SupportedLanguage EsCO = new()
    {
        CultureCode = "es-CO",
        DisplayName = "Español (Colombia)",
        NativeName  = "Español"
    };

    public override string ToString() => DisplayName;

    public static IReadOnlyList<SupportedLanguage> All =>
        new List<SupportedLanguage> { EnUS, EsCO }.AsReadOnly();
}
