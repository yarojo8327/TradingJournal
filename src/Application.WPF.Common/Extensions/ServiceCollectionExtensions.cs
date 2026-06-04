using Microsoft.Extensions.DependencyInjection;

namespace Application.WPF.Common.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddViewModels<TAssemblyMarker>(this IServiceCollection services)
    {
        var viewModelTypes = typeof(TAssemblyMarker).Assembly
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith("ViewModel"));

        foreach (var type in viewModelTypes)
            services.AddTransient(type);

        return services;
    }
}
