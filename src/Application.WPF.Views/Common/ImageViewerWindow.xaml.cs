using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace Application.WPF.Views.Common;

public partial class ImageViewerWindow : Window
{
    private double _scale = 1.0;
    private const double ZoomStep = 0.15;
    private const double MinScale = 0.2;
    private const double MaxScale = 8.0;

    public ImageViewerWindow(byte[] imageData, string? title = null)
    {
        InitializeComponent();
        TitleText.Text  = title ?? "Imagen de estrategia";
        ViewerImage.Source = LoadImage(imageData);
        UpdateZoomLabel();
    }

    private static BitmapImage? LoadImage(byte[] data)
    {
        try
        {
            var img = new BitmapImage();
            using var ms = new MemoryStream(data);
            img.BeginInit();
            img.CacheOption  = BitmapCacheOption.OnLoad;
            img.StreamSource = ms;
            img.EndInit();
            img.Freeze();
            return img;
        }
        catch { return null; }
    }

    // ── Zoom ──────────────────────────────────────────────────────────────

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        ApplyZoom(e.Delta > 0 ? ZoomStep : -ZoomStep);
        e.Handled = true;
    }

    private void OnZoomIn(object sender, RoutedEventArgs e)  => ApplyZoom(ZoomStep);
    private void OnZoomOut(object sender, RoutedEventArgs e) => ApplyZoom(-ZoomStep);

    private void OnImageDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount < 2) return;
        ResetZoom();
    }

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

    private void UpdateZoomLabel() =>
        ZoomLabel.Text = $"{(int)(_scale * 100)}%";

    // ── Cierre ────────────────────────────────────────────────────────────

    private void OnClose(object sender, RoutedEventArgs e)       => Close();
    private void OnBackgroundClick(object sender, MouseButtonEventArgs e) => Close();
    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
        if (e.Key == Key.Add   || e.Key == Key.OemPlus)  ApplyZoom(ZoomStep);
        if (e.Key == Key.Subtract || e.Key == Key.OemMinus) ApplyZoom(-ZoomStep);
        if (e.Key == Key.D0   || e.Key == Key.NumPad0)   ResetZoom();
    }
}
