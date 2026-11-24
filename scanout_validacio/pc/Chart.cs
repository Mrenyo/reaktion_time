using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;

namespace ScanoutValidacio;

// Kétpaneles ábra (PNG):
//   felül: a mért Δt (ms) a mérő négyzet függőleges helyzetének (képpontsor) függvényében, minden
//          próba halvány pontként, a pontátlagok szórással, és a scanout-képletből várt egyenes;
//   alul:  az eltérés (mért − várt, µs) pontonként: próbák, átlag és 95%-os konfidenciaintervallum.
internal static class Chart
{
    private const int Width = 1800;
    private const int Height = 1500;
    private static readonly CultureInfo Hu = CultureInfo.GetCultureInfo("hu-HU");

    public static void Draw(string path, SessionMeta meta, List<TrialRecord> records, List<PointSummary> points)
    {
        using var bmp = new Bitmap(Width, Height);
        bmp.SetResolution(200, 200);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        g.Clear(Color.White);

        var used = records
            .Where(r => r.Valid && r.DtUs is not null && Math.Abs(r.DtUs.Value - r.ExpectedDtUs) <= meta.FrameTimeUs / 2)
            .ToList();

        var top = new RectangleF(190, 60, Width - 240, 760);
        var bottom = new RectangleF(190, 980, Width - 240, 360);
        var xMax = (double)meta.ScreenHeightPx;

        // Felső panel: Δt (ms)
        double Expected(double y) => (y - meta.ReferenceCenterY) * meta.MicrosecondsPerRow / 1000;
        var yValues = used.Select(r => r.DtUs!.Value / 1000d).Concat([Expected(0), Expected(xMax)]).ToList();
        var (yMin, yMax) = NiceRange(yValues.Min(), yValues.Max());
        var axisTop = new Axis(top, 0, xMax, yMin, yMax);
        axisTop.DrawFrame(g, "A mérő négyzet középpontjának sora (px)", "Δt = t(mérő) − t(referencia) (ms)",
            NiceStep(xMax), NiceStep(yMax - yMin), "0", "0.#");

        using (var pen = new Pen(Color.FromArgb(200, 40, 40), 2.5f))
        {
            g.DrawLine(pen, axisTop.Map(0, Expected(0)), axisTop.Map(xMax, Expected(xMax)));
        }

        using (var dot = new SolidBrush(Color.FromArgb(50, 30, 90, 200)))
        {
            foreach (var r in used)
            {
                var p = axisTop.Map(r.PointY + Jitter(r.Trial, xMax * 0.006), r.DtUs!.Value / 1000d);
                g.FillEllipse(dot, p.X - 3.5f, p.Y - 3.5f, 7, 7);
            }
        }

        using (var mean = new SolidBrush(Color.FromArgb(20, 40, 120)))
        using (var font = new Font("Segoe UI", 8.5f))
        {
            foreach (var pt in points.Where(p => p.ValidCount > 0))
            {
                var c = axisTop.Map(pt.Y, pt.MeanUs / 1000);
                g.FillEllipse(mean, c.X - 7, c.Y - 7, 14, 14);
                g.DrawString(pt.Name, font, Brushes.Black, c.X + 12, c.Y - 30);
            }
        }

        DrawLegend(g, new PointF(top.Left + 20, top.Top + 15));

        // Alsó panel: eltérés (µs)
        var residuals = used.Select(r => r.DtUs!.Value - r.ExpectedDtUs).ToList();
        var (rMin, rMax) = NiceRange(Math.Min(residuals.DefaultIfEmpty(-10).Min(), -10), Math.Max(residuals.DefaultIfEmpty(10).Max(), 10));
        var names = points.Select(p => p.Name).ToList();
        var axisBottom = new Axis(bottom, -0.5, names.Count - 0.5, rMin, rMax);
        axisBottom.DrawFrame(g, "Mérési pont", "Mért − várt (µs)", 0, NiceStep(rMax - rMin), "0", "0");
        using (var zero = new Pen(Color.FromArgb(200, 40, 40), 1.5f) { DashStyle = DashStyle.Dash })
        {
            g.DrawLine(zero, axisBottom.Map(-0.5, 0), axisBottom.Map(names.Count - 0.5, 0));
        }

        using (var dot = new SolidBrush(Color.FromArgb(50, 30, 90, 200)))
        using (var mean = new SolidBrush(Color.FromArgb(20, 40, 120)))
        using (var ci = new Pen(Color.FromArgb(20, 40, 120), 2.5f))
        using (var font = new Font("Segoe UI", 8.5f))
        {
            for (var i = 0; i < names.Count; i++)
            {
                var pt = points[i];
                foreach (var r in used.Where(r => r.Point == pt.Name))
                {
                    var p = axisBottom.Map(i + Jitter(r.Trial, 0.18), r.DtUs!.Value - r.ExpectedDtUs);
                    g.FillEllipse(dot, p.X - 3.5f, p.Y - 3.5f, 7, 7);
                }

                var label = $"{pt.Name}\n(y = {pt.Y})";
                var size = g.MeasureString(label, font);
                var at = axisBottom.Map(i, rMin);
                g.DrawString(label, font, Brushes.Black, at.X - (size.Width / 2), at.Y + 8);
                if (pt.ValidCount == 0)
                {
                    continue;
                }

                var c = axisBottom.Map(i + 0.3, pt.ResidualMeanUs);
                if (double.IsFinite(pt.ResidualCi95Us))
                {
                    var lo = axisBottom.Map(i + 0.3, pt.ResidualMeanUs - pt.ResidualCi95Us);
                    var hi = axisBottom.Map(i + 0.3, pt.ResidualMeanUs + pt.ResidualCi95Us);
                    g.DrawLine(ci, lo, hi);
                    g.DrawLine(ci, lo.X - 8, lo.Y, lo.X + 8, lo.Y);
                    g.DrawLine(ci, hi.X - 8, hi.Y, hi.X + 8, hi.Y);
                }

                g.FillEllipse(mean, c.X - 7, c.Y - 7, 14, 14);
            }
        }

        bmp.Save(path, ImageFormat.Png);
    }

    private static void DrawLegend(Graphics g, PointF at)
    {
        using var font = new Font("Segoe UI", 8.5f);
        using var line = new Pen(Color.FromArgb(200, 40, 40), 2.5f);
        using var dot = new SolidBrush(Color.FromArgb(90, 30, 90, 200));
        using var mean = new SolidBrush(Color.FromArgb(20, 40, 120));
        using var border = new Pen(Color.FromArgb(200, 200, 200), 1);
        g.FillRectangle(Brushes.White, at.X - 8, at.Y - 6, 330, 118);
        g.DrawRectangle(border, at.X - 8, at.Y - 6, 330, 118);
        g.DrawLine(line, at.X, at.Y + 14, at.X + 40, at.Y + 14);
        g.DrawString("várt (scanout-képlet)", font, Brushes.Black, at.X + 50, at.Y);
        g.FillEllipse(dot, at.X + 16, at.Y + 46, 8, 8);
        g.DrawString("egy próba", font, Brushes.Black, at.X + 50, at.Y + 36);
        g.FillEllipse(mean, at.X + 13, at.Y + 80, 14, 14);
        g.DrawString("pontátlag", font, Brushes.Black, at.X + 50, at.Y + 72);
    }

    // Determinisztikus, kis vízszintes szórás, hogy az egymásra eső pontok láthatók legyenek.
    private static double Jitter(int trial, double amplitude) =>
        (((trial * 0.6180339887) % 1.0) - 0.5) * 2 * amplitude;

    private static (double, double) NiceRange(double min, double max)
    {
        if (min == max)
        {
            min -= 1;
            max += 1;
        }

        var step = NiceStep(max - min);
        return (Math.Floor(min / step) * step - (step * 0.25), Math.Ceiling(max / step) * step + (step * 0.25));
    }

    private static double NiceStep(double range)
    {
        var raw = range / 6;
        var mag = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        var norm = raw / mag;
        return (norm < 1.5 ? 1 : norm < 3 ? 2 : norm < 7 ? 5 : 10) * mag;
    }

    private sealed class Axis(RectangleF area, double x0, double x1, double y0, double y1)
    {
        public PointF Map(double x, double y) => new(
            (float)(area.Left + ((x - x0) / (x1 - x0) * area.Width)),
            (float)(area.Bottom - ((y - y0) / (y1 - y0) * area.Height)));

        public void DrawFrame(Graphics g, string xLabel, string yLabel, double xStep, double yStep, string xFormat, string yFormat)
        {
            using var grid = new Pen(Color.FromArgb(225, 225, 225), 1);
            using var frame = new Pen(Color.FromArgb(90, 90, 90), 1.5f);
            using var font = new Font("Segoe UI", 8.5f);
            using var labelFont = new Font("Segoe UI", 9.5f, FontStyle.Bold);

            for (var y = Math.Ceiling(y0 / yStep) * yStep; y <= y1; y += yStep)
            {
                var p = Map(x0, y);
                g.DrawLine(grid, area.Left, p.Y, area.Right, p.Y);
                var text = (Math.Abs(y) < yStep / 1000 ? 0 : y).ToString(yFormat, Hu);
                var size = g.MeasureString(text, font);
                g.DrawString(text, font, Brushes.Black, area.Left - size.Width - 6, p.Y - (size.Height / 2));
            }

            if (xStep > 0)
            {
                for (var x = Math.Ceiling(x0 / xStep) * xStep; x <= x1; x += xStep)
                {
                    var p = Map(x, y0);
                    g.DrawLine(grid, p.X, area.Top, p.X, area.Bottom);
                    var text = x.ToString(xFormat, Hu);
                    var size = g.MeasureString(text, font);
                    g.DrawString(text, font, Brushes.Black, p.X - (size.Width / 2), area.Bottom + 6);
                }
            }

            g.DrawRectangle(frame, area.Left, area.Top, area.Width, area.Height);

            var xl = g.MeasureString(xLabel, labelFont);
            g.DrawString(xLabel, labelFont, Brushes.Black, area.Left + ((area.Width - xl.Width) / 2), area.Bottom + (xStep > 0 ? 50 : 95));

            var state = g.Save();
            var yl = g.MeasureString(yLabel, labelFont);
            g.TranslateTransform(area.Left - 150, area.Top + ((area.Height + yl.Width) / 2));
            g.RotateTransform(-90);
            g.DrawString(yLabel, labelFont, Brushes.Black, 0, 0);
            g.Restore(state);
        }
    }
}
