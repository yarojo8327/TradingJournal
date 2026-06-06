using Application.WPF.ViewModels.Playbook;
using Microsoft.Win32;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Application.WPF.Views.Playbook;

public partial class PlaybookFormWindow : Window
{
    private readonly PlaybookViewModel _vm;

    private double _scale    = 1.0;
    private const double ZoomStep = 0.15;
    private const double MinScale = 0.1;
    private const double MaxScale = 8.0;

    public PlaybookFormWindow(PlaybookViewModel vm)
    {
        _vm = vm;
        InitializeComponent();
        DataContext = vm;
        _vm.PropertyChanged += OnVmPropertyChanged;
    }

    // ── Reaccionar a cambios del ViewModel ───────────────────────────────

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlaybookViewModel.IsFormVisible) && !_vm.IsFormVisible)
            Dispatcher.InvokeAsync(Close);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _vm.PropertyChanged -= OnVmPropertyChanged;
        if (_vm.IsFormVisible)
            _vm.CancelCommand.Execute(null);
        base.OnClosing(e);
    }

    // ── Arrastre de la ventana ───────────────────────────────────────────

    private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    // ── Selección de imagen desde disco ─────────────────────────────────

    private void OnBrowseImage(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title       = "Seleccionar imagen del gráfico",
            Filter      = "Imágenes|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp|Todos los archivos|*.*",
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

            _vm.SetImage(bytes, mimeType);
            ResetZoom();   // restablecer zoom al cargar imagen nueva
        }
        catch (Exception ex)
        {
            MessageBox.Show($"No se pudo cargar la imagen:\n{ex.Message}",
                            "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // ── Panning (arrastrar con ratón) ────────────────────────────────────

    private bool  _isPanning;
    private Point _panStart;
    private double _panStartH;
    private double _panStartV;

    private void OnImageMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;

        if (e.ClickCount >= 2) { ResetZoom(); return; }

        _isPanning  = true;
        _panStart   = e.GetPosition(ImageScrollViewer);
        _panStartH  = ImageScrollViewer.HorizontalOffset;
        _panStartV  = ImageScrollViewer.VerticalOffset;

        ((FrameworkElement)sender).CaptureMouse();
        ((FrameworkElement)sender).Cursor = Cursors.SizeAll;
        e.Handled = true;
    }

    private void OnImageMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isPanning) return;

        var pos   = e.GetPosition(ImageScrollViewer);
        var delta = pos - _panStart;

        ImageScrollViewer.ScrollToHorizontalOffset(_panStartH - delta.X);
        ImageScrollViewer.ScrollToVerticalOffset(_panStartV - delta.Y);
        e.Handled = true;
    }

    private void OnImageMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || !_isPanning) return;

        _isPanning = false;
        ((FrameworkElement)sender).ReleaseMouseCapture();
        ((FrameworkElement)sender).Cursor = Cursors.Hand;
        e.Handled = true;
    }

    // ── Zoom ─────────────────────────────────────────────────────────────

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        ApplyZoom(e.Delta > 0 ? ZoomStep : -ZoomStep);
        e.Handled = true;
    }

    private void OnZoomIn(object sender, RoutedEventArgs e)    => ApplyZoom(ZoomStep);
    private void OnZoomOut(object sender, RoutedEventArgs e)   => ApplyZoom(-ZoomStep);
    private void OnZoomReset(object sender, RoutedEventArgs e) => ResetZoom();

    private void ApplyZoom(double delta)
    {
        _scale = Math.Clamp(_scale + delta, MinScale, MaxScale);
        ImageScale.ScaleX = _scale;
        ImageScale.ScaleY = _scale;
        UpdateZoomLabel();
    }

    private void ResetZoom()
    {
        _scale = 1.0;
        ImageScale.ScaleX = 1.0;
        ImageScale.ScaleY = 1.0;
        UpdateZoomLabel();
    }

    private void UpdateZoomLabel() => ZoomLabel.Text = $"{(int)(_scale * 100)}%";
}
