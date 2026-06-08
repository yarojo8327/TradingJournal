using Application.WPF.ViewModels.Journal;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Application.WPF.Views.Journal;

public partial class TradeJournalView : UserControl
{
    private TradeFormWindow? _formWindow;

    public TradeJournalView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is TradeJournalViewModel old)
        {
            old.PropertyChanged -= OnVmPropertyChanged;
            old.StatsUpdated    -= OnStatsUpdated;
        }
        if (e.NewValue is TradeJournalViewModel vm)
        {
            vm.PropertyChanged += OnVmPropertyChanged;
            vm.StatsUpdated    += OnStatsUpdated;
        }
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TradeJournalViewModel.IsFormVisible)) return;
        if (DataContext is not TradeJournalViewModel vm) return;

        if (vm.IsFormVisible)
        {
            Dispatcher.InvokeAsync(() =>
            {
                var owner = Window.GetWindow(this);
                _formWindow = new TradeFormWindow(vm) { Owner = owner };
                _formWindow.ShowDialog();
                _formWindow = null;
            });
        }
        else
        {
            _formWindow?.Close();
        }
    }

    private void OnStatsUpdated(object? sender, EventArgs e)
        => Dispatcher.InvokeAsync(RedrawAllCharts);

    private void RedrawAllCharts()
    {
        DrawEquityCurve();
        DrawPnlBars();
        DrawPieChart();
    }

    // ── SizeChanged handlers ──────────────────────────────────────────────

    private void StatsEquityCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => DrawEquityCurve();
    private void PnlBarsCanvas_SizeChanged(object sender, SizeChangedEventArgs e)     => DrawPnlBars();
    private void PieCanvas_SizeChanged(object sender, SizeChangedEventArgs e)         => DrawPieChart();

    // ═════════════════════════════════════════════════════════════════════
    // EQUITY CURVE
    // ═════════════════════════════════════════════════════════════════════

    private void DrawEquityCurve()
    {
        if (DataContext is not TradeJournalViewModel vm) return;
        var canvas = StatsEquityCanvas;
        canvas.Children.Clear();

        var pts = vm.EquityPoints;
        if (pts.Count < 2) { DrawNoDataLabel(canvas, "Sin datos"); return; }

        const double padL = 52, padR = 8, padT = 8, padB = 22;
        double w = canvas.ActualWidth;
        double h = canvas.ActualHeight;
        if (w < 40 || h < 40) return;

        double min = pts.Min();
        double max = pts.Max();
        double range = max - min;
        if (range == 0) range = 1;

        double chartW = w - padL - padR;
        double chartH = h - padT - padB;

        double ToX(int i)  => padL + i * chartW / (pts.Count - 1);
        double ToY(double v) => padT + (1 - (v - min) / range) * chartH;

        // Gridlines + Y-axis labels (4 levels)
        for (int k = 0; k <= 3; k++)
        {
            double frac = k / 3.0;
            double val  = min + frac * range;
            double y    = padT + (1 - frac) * chartH;

            var line = new Line
            {
                X1 = padL, X2 = w - padR, Y1 = y, Y2 = y,
                Stroke = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                StrokeThickness = 1
            };
            canvas.Children.Add(line);

            var lbl = new TextBlock
            {
                Text       = FormatVal(val),
                FontSize   = 9,
                Foreground = new SolidColorBrush(Color.FromArgb(120, 200, 200, 200))
            };
            Canvas.SetRight(lbl, w - padL + 2);
            Canvas.SetTop(lbl, y - 7);
            canvas.Children.Add(lbl);
        }

        // Filled area
        bool isPositive = pts[pts.Count - 1] >= pts[0];
        var areaColor   = isPositive ? Color.FromArgb(35, 0, 230, 118) : Color.FromArgb(35, 255, 80, 80);
        double baseY    = ToY(min);

        var areaSeg = new PathFigure { StartPoint = new Point(ToX(0), baseY) };
        areaSeg.Segments.Add(new LineSegment(new Point(ToX(0), ToY(pts[0])), false));
        for (int i = 1; i < pts.Count; i++)
            areaSeg.Segments.Add(new LineSegment(new Point(ToX(i), ToY(pts[i])), true));
        areaSeg.Segments.Add(new LineSegment(new Point(ToX(pts.Count - 1), baseY), false));
        areaSeg.IsClosed = true;

        canvas.Children.Add(new Path
        {
            Data = new PathGeometry(new[] { areaSeg }),
            Fill = new SolidColorBrush(areaColor),
            Stroke = Brushes.Transparent
        });

        // Line
        var polyline = new Polyline
        {
            Stroke          = new SolidColorBrush(isPositive ? Color.FromRgb(0, 230, 118) : Color.FromRgb(255, 80, 80)),
            StrokeThickness = 2,
            StrokeLineJoin  = PenLineJoin.Round
        };
        for (int i = 0; i < pts.Count; i++)
            polyline.Points.Add(new Point(ToX(i), ToY(pts[i])));
        canvas.Children.Add(polyline);

        // Endpoint dot + label
        double ex = ToX(pts.Count - 1), ey = ToY(pts[pts.Count - 1]);
        canvas.Children.Add(new Ellipse
        {
            Width = 7, Height = 7,
            Fill = new SolidColorBrush(isPositive ? Color.FromRgb(0, 230, 118) : Color.FromRgb(255, 80, 80))
        });
        Canvas.SetLeft(canvas.Children[^1], ex - 3.5);
        Canvas.SetTop(canvas.Children[^1],  ey - 3.5);

        var endLbl = new TextBlock
        {
            Text       = FormatVal(pts[pts.Count - 1]),
            FontSize   = 9,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(isPositive ? Color.FromRgb(0, 230, 118) : Color.FromRgb(255, 80, 80))
        };
        Canvas.SetLeft(endLbl, ex + 6);
        Canvas.SetTop(endLbl, ey - 7);
        canvas.Children.Add(endLbl);
    }

    // ═════════════════════════════════════════════════════════════════════
    // P&L BAR CHART
    // ═════════════════════════════════════════════════════════════════════

    private void DrawPnlBars()
    {
        if (DataContext is not TradeJournalViewModel vm) return;
        var canvas = PnlBarsCanvas;
        canvas.Children.Clear();

        var pts = vm.PnlPoints;
        if (pts.Count == 0) { DrawNoDataLabel(canvas, "Sin operaciones"); return; }

        const double padL = 46, padR = 6, padT = 8, padB = 6;
        double w = canvas.ActualWidth;
        double h = canvas.ActualHeight;
        if (w < 40 || h < 40) return;

        double chartW = w - padL - padR;
        double chartH = h - padT - padB;

        double maxAbs = pts.Max(Math.Abs);
        if (maxAbs == 0) maxAbs = 1;

        // Determine axis-zero position
        double minV  = pts.Min();
        double maxV  = pts.Max();
        double range = maxV - minV;
        if (range == 0) range = 1;

        double zeroY = padT + (maxV / (maxV - minV)) * chartH;
        if (minV >= 0) zeroY = padT + chartH;   // all positive
        if (maxV <= 0) zeroY = padT;             // all negative

        // Zero line
        canvas.Children.Add(new Line
        {
            X1 = padL, X2 = w - padR, Y1 = zeroY, Y2 = zeroY,
            Stroke = new SolidColorBrush(Color.FromArgb(80, 200, 200, 200)),
            StrokeThickness = 1
        });

        // Y-axis labels (max, 0, min)
        void AddYLbl(double val, double y)
        {
            var lbl = new TextBlock
            {
                Text       = FormatVal(val),
                FontSize   = 8,
                Foreground = new SolidColorBrush(Color.FromArgb(120, 200, 200, 200))
            };
            Canvas.SetRight(lbl, w - padL + 2);
            Canvas.SetTop(lbl, y - 6);
            canvas.Children.Add(lbl);
        }
        AddYLbl(maxV, padT);
        if (minV < 0) AddYLbl(0, zeroY);
        AddYLbl(minV, padT + chartH - 6);

        // Bars
        double barW   = Math.Max(1, chartW / pts.Count - 1.5);
        double step   = chartW / pts.Count;

        for (int i = 0; i < pts.Count; i++)
        {
            double val = pts[i];
            double x   = padL + i * step + step / 2 - barW / 2;

            double barH, barTop;
            if (val >= 0)
            {
                barH   = (val / (maxV - (minV < 0 ? minV : 0))) * chartH;
                barTop = zeroY - barH;
                if (minV >= 0) { barH = (val / maxV) * chartH; barTop = padT + chartH - barH; }
            }
            else
            {
                barH   = (Math.Abs(val) / (maxV > 0 ? (maxV - minV) : Math.Abs(minV))) * chartH;
                barTop = zeroY;
                if (maxV <= 0) { barH = (Math.Abs(val) / Math.Abs(minV)) * chartH; barTop = padT + chartH - barH; }
            }

            barH = Math.Max(1, barH);

            var bar = new Rectangle
            {
                Width            = barW,
                Height           = barH,
                Fill             = new SolidColorBrush(val >= 0
                                       ? Color.FromArgb(200, 0, 200, 100)
                                       : Color.FromArgb(200, 220, 60, 60)),
                RadiusX          = 1.5, RadiusY = 1.5
            };
            Canvas.SetLeft(bar, x);
            Canvas.SetTop(bar, barTop);
            canvas.Children.Add(bar);
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    // PIE CHART — Ganancia vs Pérdida
    // ═════════════════════════════════════════════════════════════════════

    private void DrawPieChart()
    {
        if (DataContext is not TradeJournalViewModel vm) return;
        var canvas = PieCanvas;
        canvas.Children.Clear();

        int won  = vm.StatsWonCount;
        int lost = vm.StatsLostCount;
        int total = won + lost;
        if (total == 0) { DrawNoDataLabel(canvas, "Sin datos"); return; }

        double w = canvas.ActualWidth;
        double h = canvas.ActualHeight;
        if (w < 20 || h < 20) return;

        // Layout: pie left, legend right
        double legendW  = 70;
        double pieArea  = Math.Min(w - legendW - 8, h);
        double cx       = pieArea / 2;
        double cy       = h / 2;
        double r        = Math.Min(cx, cy) * 0.88;

        double wonAngle  = 360.0 * won  / total;
        double lostAngle = 360.0 * lost / total;

        DrawSlice(canvas, cx, cy, r, -90, wonAngle,  Color.FromRgb(70, 130, 200));   // Blue = wins
        DrawSlice(canvas, cx, cy, r, -90 + wonAngle, lostAngle, Color.FromRgb(200, 60, 60)); // Red = losses

        // Labels inside slices
        if (won > 0)
        {
            var midA    = (-90 + wonAngle / 2) * Math.PI / 180;
            var innerR  = r * 0.55;
            var lx      = cx + innerR * Math.Cos(midA);
            var ly      = cy + innerR * Math.Sin(midA);
            AddPieLabel(canvas, won.ToString(), lx, ly);
        }
        if (lost > 0)
        {
            var midA   = (-90 + wonAngle + lostAngle / 2) * Math.PI / 180;
            var innerR = r * 0.55;
            var lx     = cx + innerR * Math.Cos(midA);
            var ly     = cy + innerR * Math.Sin(midA);
            AddPieLabel(canvas, lost.ToString(), lx, ly);
        }

        // Legend
        double lx0 = pieArea + 4;
        AddLegend(canvas, lx0, h / 2 - 20, Color.FromRgb(70, 130, 200),  "GANADAS");
        AddLegend(canvas, lx0, h / 2 + 2,  Color.FromRgb(200, 60, 60),   "PÉRDIDAS");
    }

    private static void DrawSlice(Canvas canvas, double cx, double cy, double r,
                                   double startDeg, double sweepDeg, Color color)
    {
        if (sweepDeg <= 0) return;

        bool isFullCircle = sweepDeg >= 359.99;
        double startRad = startDeg * Math.PI / 180;
        double endRad   = (startDeg + (isFullCircle ? 359.99 : sweepDeg)) * Math.PI / 180;

        var fig = new PathFigure
        {
            StartPoint = isFullCircle
                ? new Point(cx + r * Math.Cos(startRad), cy + r * Math.Sin(startRad))
                : new Point(cx, cy),
            IsClosed = true
        };

        if (!isFullCircle)
            fig.Segments.Add(new LineSegment(
                new Point(cx + r * Math.Cos(startRad), cy + r * Math.Sin(startRad)), true));

        fig.Segments.Add(new ArcSegment(
            new Point(cx + r * Math.Cos(endRad), cy + r * Math.Sin(endRad)),
            new Size(r, r), 0, sweepDeg > 180, SweepDirection.Clockwise, true));

        if (!isFullCircle)
            fig.Segments.Add(new LineSegment(new Point(cx, cy), true));

        canvas.Children.Add(new Path
        {
            Data = new PathGeometry(new[] { fig }),
            Fill = new SolidColorBrush(Color.FromArgb(200, color.R, color.G, color.B)),
            Stroke = new SolidColorBrush(Color.FromArgb(60, 0, 0, 0)),
            StrokeThickness = 1
        });
    }

    private static void AddPieLabel(Canvas c, string text, double cx, double cy)
    {
        var tb = new TextBlock
        {
            Text       = text,
            FontSize   = 11,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White
        };
        c.Children.Add(tb);
        // Position centered — measure after add
        tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(tb, cx - tb.DesiredSize.Width / 2);
        Canvas.SetTop(tb,  cy - tb.DesiredSize.Height / 2);
    }

    private static void AddLegend(Canvas c, double x, double y, Color color, string text)
    {
        var dot = new Ellipse { Width = 8, Height = 8, Fill = new SolidColorBrush(color) };
        Canvas.SetLeft(dot, x); Canvas.SetTop(dot, y + 2);
        c.Children.Add(dot);

        var tb = new TextBlock
        {
            Text       = text,
            FontSize   = 9,
            Foreground = new SolidColorBrush(Color.FromArgb(180, 200, 200, 200))
        };
        Canvas.SetLeft(tb, x + 12); Canvas.SetTop(tb, y);
        c.Children.Add(tb);
    }

    // ── Utilities ─────────────────────────────────────────────────────────

    private static void DrawNoDataLabel(Canvas canvas, string msg)
    {
        var tb = new TextBlock
        {
            Text       = msg,
            FontSize   = 11,
            Foreground = new SolidColorBrush(Color.FromArgb(80, 200, 200, 200))
        };
        canvas.Children.Add(tb);
        tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(tb, (canvas.ActualWidth  - tb.DesiredSize.Width)  / 2);
        Canvas.SetTop(tb,  (canvas.ActualHeight - tb.DesiredSize.Height) / 2);
    }

    private static string FormatVal(double v)
    {
        if (Math.Abs(v) >= 1_000_000) return $"{v / 1_000_000:F1}M";
        if (Math.Abs(v) >= 1_000)     return $"{v / 1_000:F1}K";
        return $"{v:F1}";
    }
}
