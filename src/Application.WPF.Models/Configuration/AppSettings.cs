namespace Application.WPF.Models.Configuration;

public class AppSettings
{
    public const string SectionName = "AppSettings";

    public string ApplicationName { get; set; } = "TradingJournal";
    public string Version { get; set; } = "1.0.0";
    public string Environment { get; set; } = "Production";
    public LoggingSettings Logging { get; set; } = new();
    public UISettings UI { get; set; } = new();
}

public class LoggingSettings
{
    public string MinimumLevel { get; set; } = "Information";
    public bool WriteToFile { get; set; } = true;
    public bool WriteToConsole { get; set; } = true;
}

public class UISettings
{
    public string Theme { get; set; } = "Light";
    public string Language { get; set; } = "en-US";
}
