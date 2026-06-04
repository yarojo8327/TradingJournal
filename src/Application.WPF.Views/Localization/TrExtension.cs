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

        // Design-time fallback
        if (target.TargetObject is not DependencyObject)
            return $"[{Key}]";

        var locService = TryResolveService(target.TargetObject as DependencyObject);
        if (locService is null)
            return $"[{Key}]";

        var binding = new Binding($"[{Key}]")
        {
            Source = locService,
            Mode   = BindingMode.OneWay
        };

        return binding.ProvideValue(serviceProvider);
    }

    private static ILocalizationService? TryResolveService(DependencyObject? target)
    {
        if (target is null) return null;

        // Walk up the logical tree to find the Application resources
        return System.Windows.Application.Current?.Resources["Loc"] as ILocalizationService;
    }
}
