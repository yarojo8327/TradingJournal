using Application.WPF.Services.Interfaces;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace Application.WPF.Views.Localization;

/// <summary>
/// Markup extension for localized strings.
/// Usage: Text="{loc:Tr Key=Nav_Dashboard}"
/// Automatically updates when the language changes.
/// Works in normal controls, DataTemplates and Style Setters.
/// </summary>
[MarkupExtensionReturnType(typeof(BindingBase))]
public class TrExtension : MarkupExtension
{
    [ConstructorArgument("key")]
    public string Key { get; set; } = string.Empty;

    public TrExtension() { }
    public TrExtension(string key) => Key = key;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (serviceProvider.GetService(typeof(IProvideValueTarget)) is not IProvideValueTarget pvt)
            return $"[{Key}]";

        var locService = System.Windows.Application.Current?.Resources["Loc"] as ILocalizationService;

        if (locService is null)
        {
            // Diseño o servicio aún no inicializado: diferir si es DataTemplate/Setter
            if (pvt.TargetObject is not DependencyObject)
                return this;
            return $"[{Key}]";
        }

        // Crear el binding. Binding.ProvideValue devuelve:
        //   - BindingExpression activo cuando el target es DependencyObject+DependencyProperty
        //   - el propio Binding (BindingBase) en contexto de DataTemplate o Style Setter
        // Ambos son válidos para WPF.
        var binding = new Binding($"[{Key}]")
        {
            Source = locService,
            Mode   = BindingMode.OneWay
        };

        return binding.ProvideValue(serviceProvider);
    }
}
