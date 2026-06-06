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
            old.EquityUpdated  -= OnEquityUpdated;
            old.PropertyChanged -= OnVmPropertyChanged;
        }

        if (e.NewValue is DashboardViewModel vm)
        {
            _vm = vm;
            vm.EquityUpdated  += OnEquityUpdated;
            vm.PropertyChanged += OnVmPropertyChanged;
        }
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e) { }

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

        var values = _vm?.EquityValues;
        var dates  = _vm?.EquityDates;
        if (values == null || values.Count < 2)
        {
            EquityNoData.Visibility = Visibility.Visible;
            return;
        }

        EquityNoData.Visibility = Visibility.Collapsed;

        double canvasW = EquityCanvas.ActualWidth;
        double canvasH = EquityCanvas.ActualHeight;
        if (canvasW <= 0 || canvasH <= 0) return;

        // Layout margins — leave space for Y-axis labels (left) and X-axis labels (bottom)
        const double padLeft   = 62;
        const double padRight  = 8;
        const double padTop    = 8;
        const double padBottom = 22;

        double plotW = canvasW - padLeft - padRight;
        double plotH = canvasH - padTop  - padBottom;

        double minVal = values.Min();
        double maxVal = values.Max();
        double range  = maxVal - minVal;
        if (range < 1) { range = Math.Abs(minVal) > 0 ? Math.Abs(minVal) * 0.1 : 100; }

        bool isPositive = values[^1] >= values[0];

        // ── Helper: data value → canvas Y ─────────────────────────────────
        double DataToY(double v) => padTop + (1.0 - (v - minVal) / range) * plotH;

        // ── Helper: index → canvas X ───────────────────────────────────────
        double IndexToX(int i) => padLeft + (double)i / (values.Count - 1) * plotW;

        // ── Y-axis gridlines + labels (4 levels) ──────────────────────────
        var yLevels = new[]
        {
            minVal,
            minVal + range * 0.33,
            minVal + range * 0.67,
            maxVal
        };

        foreach (var yVal in yLevels)
        {
            double cy = DataToY(yVal);

            // Gridline
            var grid = new Line
            {
                X1              = padLeft,
                X2              = canvasW - padRight,
                Y1              = cy,
                Y2              = cy,
                Stroke          = new SolidColorBrush(Color.FromArgb(25, 255, 255, 255)),
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 3, 3 },
            };
            EquityCanvas.Children.Add(grid);

            // Y label — show in K if >= 1000
            string yLabel = FormatValue(yVal);
            bool   isOver = Math.Abs(yVal - maxVal) < 0.01;
            var yText = new TextBlock
            {
                Text       = yLabel,
                FontSize   = 9,
                FontWeight = isOver ? FontWeights.Bold : FontWeights.Normal,
                Foreground = isOver
                    ? new SolidColorBrush(isPositive
                        ? Color.FromRgb(0, 230, 118)
                        : Color.FromRgb(255, 68, 68))
                    : new SolidColorBrush(Color.FromArgb(130, 200, 200, 220)),
            };
            Canvas.SetLeft(yText, 0);
            Canvas.SetTop (yText, cy - 8);
            yText.Width         = padLeft - 4;
            yText.TextAlignment = System.Windows.TextAlignment.Right;
            EquityCanvas.Children.Add(yText);
        }

        // ── Zero / baseline (initial capital) line ─────────────────────────
        double zeroY = DataToY(values[0]);
        if (zeroY >= padTop && zeroY <= padTop + plotH)
        {
            var baseLine = new Line
            {
                X1              = padLeft,
                X2              = canvasW - padRight,
                Y1              = zeroY,
                Y2              = zeroY,
                Stroke          = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 5, 4 },
            };
            EquityCanvas.Children.Add(baseLine);
        }

        // ── Build screen points ─────────────────────────────────────────────
        var screenPts = new PointCollection();
        for (int i = 0; i < values.Count; i++)
            screenPts.Add(new Point(IndexToX(i), DataToY(values[i])));

        // ── Filled gradient area ───────────────────────────────────────────
        var areaColor = isPositive
            ? Color.FromArgb(35, 0, 230, 118)
            : Color.FromArgb(35, 255, 68,  68);

        var areaFigure = new PathFigure
        {
            StartPoint = new Point(screenPts[0].X, DataToY(values[0])),
            IsClosed   = true
        };
        areaFigure.Segments.Add(new LineSegment(screenPts[0], false));
        areaFigure.Segments.Add(new PolyLineSegment(screenPts, true));
        areaFigure.Segments.Add(new LineSegment(new Point(screenPts[^1].X, DataToY(values[0])), false));

        var areaPath = new Path
        {
            Data = new PathGeometry(new[] { areaFigure }),
            Fill = new SolidColorBrush(areaColor),
        };
        EquityCanvas.Children.Add(areaPath);

        // ── Main equity line ──────────────────────────────────────────────
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

        // ── Dot at last point ─────────────────────────────────────────────
        var lastPt = screenPts[^1];
        var dot = new Ellipse { Width = 8, Height = 8, Fill = new SolidColorBrush(lineColor) };
        Canvas.SetLeft(dot, lastPt.X - 4);
        Canvas.SetTop (dot, lastPt.Y - 4);
        EquityCanvas.Children.Add(dot);

        // Current value tooltip next to dot
        var dotLabel = new TextBlock
        {
            Text       = FormatValue(values[^1]),
            FontSize   = 9,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(lineColor),
        };
        double dotLabelX = lastPt.X + 6;
        if (dotLabelX + 50 > canvasW) dotLabelX = lastPt.X - 56;
        Canvas.SetLeft(dotLabel, dotLabelX);
        Canvas.SetTop (dotLabel, lastPt.Y - 7);
        EquityCanvas.Children.Add(dotLabel);

        // ── X-axis date labels (start, mid, end) ──────────────────────────
        if (dates != null && dates.Count >= 2)
        {
            DrawDateLabel(dates[0],                  IndexToX(0),                   canvasH, padBottom, padLeft, true);
            DrawDateLabel(dates[dates.Count / 2],    IndexToX(dates.Count / 2),     canvasH, padBottom, padLeft, false);
            DrawDateLabel(dates[^1],                 IndexToX(dates.Count - 1),     canvasH, padBottom, padLeft, false);
        }
    }

    private void DrawDateLabel(DateTime date, double cx, double canvasH, double padBottom, double padLeft, bool isFirst)
    {
        var label = new TextBlock
        {
            Text      = date.ToString("dd/MM/yy"),
            FontSize  = 9,
            Foreground = new SolidColorBrush(Color.FromArgb(110, 200, 200, 220)),
        };
        double top = canvasH - padBottom + 4;
        double left = isFirst ? cx : cx - 28;
        Canvas.SetLeft(label, Math.Max(padLeft, left));
        Canvas.SetTop (label, top);
        EquityCanvas.Children.Add(label);
    }

    private static string FormatValue(double v)
    {
        if (Math.Abs(v) >= 1_000_000) return $"{v / 1_000_000:F1}M";
        if (Math.Abs(v) >= 1_000)     return $"{v / 1_000:F1}K";
        return $"{v:F0}";
    }
}
