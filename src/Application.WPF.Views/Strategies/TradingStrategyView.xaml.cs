using Application.WPF.ViewModels.Strategies;
using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace Application.WPF.Views.Strategies;

public partial class TradingStrategyView : UserControl
{
    public TradingStrategyView()
    {
        InitializeComponent();
    }

    public TradingStrategyView(TradingStrategyViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void OnBrowseImage(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title  = "Seleccionar imagen de la estrategia",
            Filter = "Imágenes|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp|Todos los archivos|*.*",
            Multiselect = false
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            var bytes    = File.ReadAllBytes(dialog.FileName);
            var mimeType = Path.GetExtension(dialog.FileName).ToLower() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png"            => "image/png",
                ".gif"            => "image/gif",
                ".bmp"            => "image/bmp",
                ".webp"           => "image/webp",
                _                 => "image/png"
            };

            if (DataContext is TradingStrategyViewModel vm)
                vm.SetImage(bytes, mimeType);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"No se pudo cargar la imagen:\n{ex.Message}",
                            "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
