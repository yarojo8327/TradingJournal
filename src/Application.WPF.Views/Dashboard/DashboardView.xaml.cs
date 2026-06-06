using Application.WPF.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Application.WPF.Views.Dashboard;

public partial class DashboardView : UserControl
{
    private DashboardViewModel? _vm;

    public DashboardView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is DashboardViewModel old)
        {
            old.EquityUpdated -= OnEquityUpdated;
            old.PropertyChanged -= OnVmPropertyChanged;
        }

        if (e.NewValue is DashboardViewModel vm)
        {
            _vm = vm;
            vm.EquityUpdated  += OnEquityUpdated;
            vm.PropertyChanged += OnVmPropertyChanged;
        }
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Re-draw win-rate bar when WinRate changes (the XAML MultiBinding handles it mostly)
        // Equity curve is triggered via EquityUpdated event
    }

    private void OnEquityUpdated(object? sender, EventArgs e)
    {
        Dispatcher.InvokeAsync(DrawEquityCurve);
    }

    private void OnEquityCanvasSizeChanged(object sender, SizeChangedEventArgs e)
    {
        DrawEquityCurve();
    }

    // ── Equity curve drawing ──────────────────────────────────────────────

    private void DrawEquityCurve()
    {
        EquityCanvas.Children.Clear();

        var points = _vm?.EquityValues;
        if (points == null || points.Count < 2)
        {
            EquityNoData.Visibility = Visibility.Visible;
            return;
        }

        EquityNoData.Visibility = Visibility.Collapsed;

        var canvasW = EquityCanvas.ActualWidth;
        var canvasH = EquityCanvas.ActualHeight;
        if (canvasW <= 0 || canvasH <= 0) return;

        const double padTop    = 10;
        const double padBottom = 10;
        const double padLeft   = 0;
        const double padRight  = 0;

        double plotW = canvasW - padLeft - padRight;
        double plotH = canvasH - padTop  - padBottom;

        double minVal = points.Min();
        double maxVal = points.Max();
        double range  = maxVal - minVal;
        if (range < 1) range = 1;

        // Build screen points
        var screenPts = new PointCollection();
        for (int i = 0; i < points.Count; i++)
        {
            double x = padLeft + (double)i / (points.Count - 1) * plotW;
            double y = padTop  + (1.0 - (points[i] - minVal) / range) * plotH;
            screenPts.Add(new Point(x, y));
        }

        // Filled area (gradient beneath the line)
        var areaFigure = new PathFigure
        {
            StartPoint = new Point(screenPts[0].X, canvasH),
            IsClosed   = true
        };
        areaFigure.Segments.Add(new LineSegment(screenPts[0], false));
        var polyLine = new PolyLineSegment(screenPts, true);
        areaFigure.Segments.Add(polyLine);
        areaFigure.Segments.Add(new LineSegment(new Point(screenPts[^1].X, canvasH), false));

        bool isPositive = (points[^1] >= points[0]);
        var  areaColor  = isPositive
            ? Color.FromArgb(40, 0, 230, 118)
            : Color.FromArgb(40, 255, 68,  68);

        var areaPath = new Path
        {
            Data = new PathGeometry(new[] { areaFigure }),
            Fill = new SolidColorBrush(areaColor),
        };
        EquityCanvas.Children.Add(areaPath);

        // Zero / baseline dashed line
        double zeroY = padTop + (1.0 - (points[0] - minVal) / range) * plotH;
        var baseLine = new Line
        {
            X1              = padLeft,
            X2              = canvasW - padRight,
            Y1              = zeroY,
            Y2              = zeroY,
            Stroke          = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 4, 4 },
        };
        EquityCanvas.Children.Add(baseLine);

        // Main line
        var lineColor = isPositive
            ? Color.FromRgb(0, 230, 118)
            : Color.FromRgb(255, 68,  68);

        var curveLine = new Polyline
        {
            Points          = screenPts,
            Stroke          = new SolidColorBrush(lineColor),
            StrokeThickness = 2,
            StrokeLineJoin  = PenLineJoin.Round,
        };
        EquityCanvas.Children.Add(curveLine);

        // Dot at the last point
        var lastPt = screenPts[^1];
        var dot = new Ellipse
        {
            Width  = 7,
            Height = 7,
            Fill   = new SolidColorBrush(lineColor),
        };
        Canvas.SetLeft(dot, lastPt.X - 3.5);
        Canvas.SetTop (dot, lastPt.Y - 3.5);
        EquityCanvas.Children.Add(dot);
    }
}
