using Application.WPF.Services.Interfaces;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace Application.WPF.Views.Localization;

/// <summary>
/// Markup extension for localized strings.
/// Usage: Text="{loc:Tr Key=Nav_Dashboard}"
/// Automatically updates when the language changes.
/// </summary>
[MarkupExtensionReturnType(typeof(string))]
public class TrExtension : MarkupExtension
{
    [ConstructorArgument("key")]
    public string Key { get; set; } = string.Empty;

    public TrExtension() { }
    public TrExtension(string key) => Key = key;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (serviceProvider.GetService(typeof(IProvideValueTarget)) is not IProvideValueTarget target)
            return $"[{Key}]";

        // Dentro de DataTemplate o Style Setter, TargetObject no es DependencyObject.
        // Retornar 'this' hace que WPF difiera la evaluación hasta que la plantilla
        // se instancie con el elemento real como destino.
        if (target.TargetObject is not DependencyObject depObj)
            return this;

        var locService = TryResolveService(depObj);
        if (locService is null)
            return $"[{Key}]";

        var binding = new Binding($"[{Key}]")
        {
            Source = locService,
            Mode   = BindingMode.OneWay
        };

        return binding.ProvideValue(serviceProvider);
    }

    private static ILocalizationService? TryResolveService(DependencyObject target)
    {
        return System.Windows.Application.Current?.Resources["Loc"] as ILocalizationService;
    }
}
