using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace Application.WPF.Views.Common;

/// <summary>
/// Imagen con zoom (rueda del mouse) y desplazamiento (clic + arrastre), igual que el visor del Playbook.
/// Doble clic para restablecer el zoom.
/// </summary>
public partial class ZoomableImage : UserControl
{
    public static readonly DependencyProperty SourceProperty =
        DependencyProperty.Register(nameof(Source), typeof(BitmapImage), typeof(ZoomableImage));

    public BitmapImage? Source
    {
        get => (BitmapImage?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    private double _scale = 1.0;
    private const double ZoomStep = 0.15;
    private const double MinScale = 0.1;
    private const double MaxScale = 8.0;

    public ZoomableImage() => InitializeComponent();

    // ── Panning (arrastrar con ratón) ────────────────────────────────────

    private bool   _isPanning;
    private Point  _panStart;
    private double _panStartH;
    private double _panStartV;

    private void OnImageMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;

        if (e.ClickCount >= 2) { ResetZoom(); return; }

        _isPanning = true;
        _panStart  = e.GetPosition(ImageScrollViewer);
        _panStartH = ImageScrollViewer.HorizontalOffset;
        _panStartV = ImageScrollViewer.VerticalOffset;

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

    private void ApplyZoom(double delta)
    {
        _scale = Math.Clamp(_scale + delta, MinScale, MaxScale);
        ImageScale.ScaleX = _scale;
        ImageScale.ScaleY = _scale;
        ZoomLabel.Text = $"{(int)(_scale * 100)}%";
    }

    private void ResetZoom()
    {
        _scale = 1.0;
        ImageScale.ScaleX = 1.0;
        ImageScale.ScaleY = 1.0;
        ZoomLabel.Text = "100%";
    }
}
