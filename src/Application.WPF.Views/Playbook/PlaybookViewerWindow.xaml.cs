using Application.WPF.Models.Entities;
using Application.WPF.ViewModels.Playbook;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Application.WPF.Views.Playbook;

public partial class PlaybookViewerWindow : Window
{
    private double _scale    = 1.0;
    private const double ZoomStep = 0.15;
    private const double MinScale = 0.1;
    private const double MaxScale = 8.0;

    public PlaybookViewerWindow(PlaybookEntry entry)
    {
        InitializeComponent();
        PopulateDetails(entry);
    }

    // ── Poblar datos del entry ────────────────────────────────────────────

    private void PopulateDetails(PlaybookEntry entry)
    {
        // Título en la barra
        TitleSymbol.Text   = entry.Symbol;
        TitleStrategy.Text = entry.Strategy is not null ? $"— {entry.Strategy.Title}" : string.Empty;

        // Panel izquierdo: imagen
        if (entry.HasImage)
        {
            ViewerImage.Source      = LoadBitmap(entry.ImageData!);
            ImageScrollViewer.Visibility = Visibility.Visible;
            NoImagePlaceholder.Visibility = Visibility.Collapsed;
        }

        // Panel derecho: detalles
        DetailSymbol.Text   = entry.Symbol;
        DetailStrategy.Text = entry.Strategy?.Title ?? "Sin estrategia";
        DateText.Text       = entry.CreatedAt.ToString("dd/MM/yyyy  HH:mm");

        // Estrellas calificación manual
        BuildStarDisplay(entry.ManualRating);
        ManualRatingText.Text = entry.ManualRating.HasValue ? $"{entry.ManualRating}/10" : "—";

        // Confluencias
        if (entry.ConfluenceRatings.Any())
        {
            var avg = entry.ConfluenceRatings.Average(r => r.Rating);
            ConfluenceAvgText.Text       = $"⚡ {avg:F1} / 10";
            ConfluenceAvgText.Foreground = avg >= 6.5
                ? new SolidColorBrush(Color.FromRgb(0, 230, 118))
                : new SolidColorBrush(Color.FromRgb(255, 152, 0));
            ConfluenceBadge.Background   = avg >= 6.5
                ? new SolidColorBrush(Color.FromArgb(26, 0, 230, 118))
                : new SolidColorBrush(Color.FromArgb(21, 255, 152, 0));

            ConfluenceList.ItemsSource = entry.ConfluenceRatings
                .OrderBy(r => r.OrderIndex)
                .ToList();
        }
        else
        {
            ConfluenceHeader.Visibility      = Visibility.Collapsed;
            ConfluenceRatingPanel.Visibility = Visibility.Collapsed;
            ConfluenceList.Visibility        = Visibility.Collapsed;
        }

        // Notas
        if (!string.IsNullOrWhiteSpace(entry.Notes))
            NotesText.Text = entry.Notes;
        else
        {
            NotesHeader.Visibility = Visibility.Collapsed;
            (NotesText.Parent as Border)!.Visibility = Visibility.Collapsed;
        }
    }

    private void BuildStarDisplay(int? rating)
    {
        for (int i = 1; i <= 10; i++)
        {
            StarDisplay.Items.Add(new TextBlock
            {
                Text       = i <= (rating ?? 0) ? "★" : "☆",
                FontSize   = 16,
                Foreground = i <= (rating ?? 0)
                    ? new SolidColorBrush(Color.FromRgb(255, 215, 0))
                    : new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin     = new Thickness(0, 0, 1, 0)
            });
        }
    }

    private static BitmapImage? LoadBitmap(byte[] data)
    {
        try
        {
            var bmp = new BitmapImage();
            using var ms = new MemoryStream(data);
            bmp.BeginInit();
            bmp.CacheOption  = BitmapCacheOption.OnLoad;
            bmp.StreamSource = ms;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch { return null; }
    }

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

    // ── Cierre y arrastre ────────────────────────────────────────────────

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
        if (e.Key == Key.Add    || e.Key == Key.OemPlus)  ApplyZoom(ZoomStep);
        if (e.Key == Key.Subtract || e.Key == Key.OemMinus) ApplyZoom(-ZoomStep);
        if (e.Key == Key.D0     || e.Key == Key.NumPad0)  ResetZoom();
    }

    private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }
}
