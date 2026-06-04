using Application.WPF.Models.Entities;
using Application.WPF.ViewModels.Strategies;
using Application.WPF.Views.Common;
using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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

    // ── Imagen: selección desde disco ────────────────────────────────────

    private void OnBrowseImage(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title       = "Seleccionar imagen de la estrategia",
            Filter      = "Imágenes|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp|Todos los archivos|*.*",
            Multiselect = false
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            var bytes    = File.ReadAllBytes(dialog.FileName);
            var mimeType = Path.GetExtension(dialog.FileName).ToLowerInvariant() switch
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

    // ── Imagen: visor ampliado desde la preview del formulario ───────────

    private void OnFormImageClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not TradingStrategyViewModel vm) return;
        if (vm.ImageData is not { Length: > 0 } data) return;

        OpenViewer(data, vm.Title);
    }

    // ── Imagen: visor ampliado desde la tarjeta en la lista ─────────────

    private void OnCardImageClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { Tag: TradingStrategy strategy }
            && strategy.ImageData is { Length: > 0 } data)
        {
            OpenViewer(data, strategy.Title);
        }
    }

    private static void OpenViewer(byte[] data, string title)
    {
        var viewer = new ImageViewerWindow(data, title)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        viewer.ShowDialog();
    }
}
