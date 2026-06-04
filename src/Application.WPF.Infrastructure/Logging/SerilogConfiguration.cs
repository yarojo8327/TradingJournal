using Application.WPF.Common.Constants;
using Serilog;
using Serilog.Events;

namespace Application.WPF.Infrastructure.Logging;

public static class SerilogConfiguration
{
    public static LoggerConfiguration CreateDefault(string minimumLevel = "Information")
    {
        var level = Enum.TryParse<LogEventLevel>(minimumLevel, out var parsed)
            ? parsed
            : LogEventLevel.Information;

        return new LoggerConfiguration()
            .MinimumLevel.Is(level)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", AppConstants.AppName)
            .Enrich.WithProperty("Version", AppConstants.AppVersion)
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                AppConstants.Logs.FileTemplate,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}");
    }
}
