using Application.WPF.Models.Configuration;

namespace Application.WPF.Services.Interfaces;

public interface IConfigurationService
{
    AppSettings Settings { get; }
    T GetSection<T>(string sectionName) where T : new();
}
