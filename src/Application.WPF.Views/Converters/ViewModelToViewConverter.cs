using Application.WPF.Common.ViewModels;
using System.Globalization;
using System.Windows.Data;

namespace Application.WPF.Views.Converters;

public class ViewModelToViewConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null) return null;

        var viewModelType = value.GetType();
        var viewTypeName = viewModelType.FullName!
            .Replace(".ViewModels.", ".Views.")
            .Replace("ViewModel", "View");

        var viewType = viewModelType.Assembly
            .GetType(viewTypeName)
            ?? AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .FirstOrDefault(t => t.Name == viewModelType.Name.Replace("ViewModel", "View"));

        if (viewType is null) return null;

        var view = Activator.CreateInstance(viewType);
        if (view is System.Windows.FrameworkElement element)
            element.DataContext = value;

        return view;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
