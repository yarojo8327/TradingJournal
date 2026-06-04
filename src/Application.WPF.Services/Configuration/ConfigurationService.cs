using Application.WPF.Models.Configuration;
using Application.WPF.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Application.WPF.Services.Configuration;

public class ConfigurationService : IConfigurationService
{
    private readonly IConfiguration _configuration;

    public AppSettings Settings { get; }

    public ConfigurationService(IOptions<AppSettings> settings, IConfiguration configuration)
    {
        Settings = settings.Value;
        _configuration = configuration;
    }

    public T GetSection<T>(string sectionName) where T : new()
    {
        var instance = new T();
        _configuration.GetSection(sectionName).Bind(instance);
        return instance;
    }
}
