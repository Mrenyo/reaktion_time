using System.Drawing.Drawing2D;
using System.ComponentModel;

namespace ReactionTimeMeasurementApp;

public enum MenuIllustrationKind
{
    Calibration,
    Measure,
    Statistics
}

public class MenuIllustrationPanel : Control
{
    private MenuIllustrationKind _kind;

    [DefaultValue(MenuIllustrationKind.Calibration)]
    public MenuIllustrationKind Kind
    {
        get => _kind;
        set
        {
            _kind = value;
            Invalidate();
        }
    }

    public MenuIllustrationPanel()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.UserPaint |
            ControlStyles.SupportsTransparentBackColor,
            true);

        BackColor = Color.Transparent;
        Size = new Size(280, 190);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        switch (Kind)
        {
            case MenuIllustrationKind.Calibration:
                DrawCalibration(e.Graphics);
                break;
            case MenuIllustrationKind.Measure:
                DrawMeasure(e.Graphics);
                break;
            case MenuIllustrationKind.Statistics:
                DrawStatistics(e.Graphics);
                break;
        }
    }

    private void DrawCalibration(Graphics g)
    {
        using var glowBrush = new PathGradientBrush(new[]
        {
            new PointF(Width * 0.18f, Height * 0.58f),
            new PointF(Width * 0.5f, Height * 0.14f),
            new PointF(Width * 0.82f, Height * 0.58f),
            new PointF(Width * 0.5f, Height * 0.9f)
        })
        {
            CenterColor = Color.FromArgb(255, 232, 238, 244),
            SurroundColors = [Color.FromArgb(0, 232, 238, 244)]
        };
        g.FillEllipse(glowBrush, Width * 0.08f, Height * 0.06f, Width * 0.84f, Height * 0.82f);

        using var eyePath = new GraphicsPath();
        eyePath.AddBezier(
            new PointF(Width * 0.18f, Height * 0.55f),
            new PointF(Width * 0.32f, Height * 0.24f),
            new PointF(Width * 0.68f, Height * 0.24f),
            new PointF(Width * 0.82f, Height * 0.55f));
        eyePath.AddBezier(
            new PointF(Width * 0.82f, Height * 0.55f),
            new PointF(Width * 0.68f, Height * 0.82f),
            new PointF(Width * 0.32f, Height * 0.82f),
            new PointF(Width * 0.18f, Height * 0.55f));

        using var eyeBrush = new LinearGradientBrush(
            new PointF(0, Height * 0.25f),
            new PointF(0, Height * 0.82f),
            Color.FromArgb(246, 249, 255),
            Color.FromArgb(214, 222, 236));
        g.FillPath(eyeBrush, eyePath);

        using var lidPen = new Pen(Color.FromArgb(105, 130, 158), 3f);
        g.DrawPath(lidPen, eyePath);

        var irisRect = new RectangleF(Width * 0.39f, Height * 0.34f, Width * 0.22f, Width * 0.22f);
        using var irisBrush = new PathGradientBrush(new[]
        {
            new PointF(irisRect.Left, irisRect.Top),
            new PointF(irisRect.Right, irisRect.Top),
            new PointF(irisRect.Right, irisRect.Bottom),
            new PointF(irisRect.Left, irisRect.Bottom)
        })
        {
            CenterColor = Color.FromArgb(216, 241, 255),
            SurroundColors = [Color.FromArgb(66, 104, 145), Color.FromArgb(66, 104, 145), Color.FromArgb(43, 86, 127), Color.FromArgb(43, 86, 127)]
        };
        g.FillEllipse(irisBrush, irisRect);

        using var pupilBrush = new SolidBrush(Color.FromArgb(24, 39, 58));
        g.FillEllipse(pupilBrush, Width * 0.465f, Height * 0.42f, Width * 0.07f, Width * 0.07f);
        using var highlightBrush = new SolidBrush(Color.FromArgb(240, 255, 255, 255));
        g.FillEllipse(highlightBrush, Width * 0.55f, Height * 0.35f, Width * 0.06f, Width * 0.06f);

        using var focusPen = new Pen(Color.FromArgb(255, 101, 52), 2f);
        DrawCorner(g, focusPen, Width * 0.08f, Height * 0.3f, true, true);
        DrawCorner(g, focusPen, Width * 0.92f, Height * 0.3f, false, true);
        DrawCorner(g, focusPen, Width * 0.08f, Height * 0.82f, true, false);
        DrawCorner(g, focusPen, Width * 0.92f, Height * 0.82f, false, false);
    }

    private void DrawMeasure(Graphics g)
    {
        using var glowBrush = new LinearGradientBrush(
            new PointF(Width * 0.1f, Height * 0.15f),
            new PointF(Width * 0.9f, Height * 0.85f),
            Color.FromArgb(255, 255, 237, 230),
            Color.FromArgb(190, 255, 226, 213));
        g.FillEllipse(glowBrush, Width * 0.05f, Height * 0.05f, Width * 0.9f, Height * 0.8f);

        using var platformBrush = new SolidBrush(Color.FromArgb(230, 216, 207, 214));
        g.FillEllipse(platformBrush, Width * 0.3f, Height * 0.58f, Width * 0.38f, Height * 0.2f);

        using var baseBrush = new LinearGradientBrush(
            new PointF(Width * 0.32f, Height * 0.5f),
            new PointF(Width * 0.68f, Height * 0.78f),
            Color.FromArgb(241, 231, 236),
            Color.FromArgb(199, 174, 184));
        g.FillEllipse(baseBrush, Width * 0.26f, Height * 0.44f, Width * 0.42f, Height * 0.28f);

        using var buttonBrush = new LinearGradientBrush(
            new PointF(Width * 0.32f, Height * 0.45f),
            new PointF(Width * 0.65f, Height * 0.72f),
            Color.FromArgb(255, 99, 58),
            Color.FromArgb(226, 47, 28));
        g.FillEllipse(buttonBrush, Width * 0.31f, Height * 0.48f, Width * 0.32f, Height * 0.18f);

        using var ringPen = new Pen(Color.FromArgb(255, 201, 161), 3f);
        g.DrawEllipse(ringPen, Width * 0.39f, Height * 0.53f, Width * 0.16f, Height * 0.08f);
        g.DrawEllipse(ringPen, Width * 0.43f, Height * 0.56f, Width * 0.08f, Height * 0.04f);

        using var targetPen = new Pen(Color.FromArgb(248, 98, 49), 6f);
        g.DrawEllipse(targetPen, Width * 0.63f, Height * 0.22f, Width * 0.26f, Width * 0.26f);
        g.DrawEllipse(targetPen, Width * 0.69f, Height * 0.28f, Width * 0.14f, Width * 0.14f);
        g.DrawLine(targetPen, Width * 0.76f, Height * 0.2f, Width * 0.76f, Height * 0.5f);
        g.DrawLine(targetPen, Width * 0.61f, Height * 0.35f, Width * 0.91f, Height * 0.35f);

        using var fingerBrush = new LinearGradientBrush(
            new PointF(Width * 0.72f, Height * 0.52f),
            new PointF(Width * 0.87f, Height * 0.84f),
            Color.FromArgb(255, 224, 201),
            Color.FromArgb(245, 185, 161));
        using var fingerPath = new GraphicsPath();
        fingerPath.AddClosedCurve([
            new PointF(Width * 0.72f, Height * 0.78f),
            new PointF(Width * 0.69f, Height * 0.62f),
            new PointF(Width * 0.73f, Height * 0.5f),
            new PointF(Width * 0.79f, Height * 0.43f),
            new PointF(Width * 0.84f, Height * 0.47f),
            new PointF(Width * 0.83f, Height * 0.69f),
            new PointF(Width * 0.79f, Height * 0.82f)
        ]);
        g.FillPath(fingerBrush, fingerPath);
    }

    private void DrawStatistics(Graphics g)
    {
        using var glowBrush = new LinearGradientBrush(
            new PointF(Width * 0.12f, Height * 0.1f),
            new PointF(Width * 0.88f, Height * 0.84f),
            Color.FromArgb(255, 255, 236, 231),
            Color.FromArgb(120, 255, 221, 215));
        g.FillEllipse(glowBrush, Width * 0.08f, Height * 0.1f, Width * 0.84f, Height * 0.75f);

        using var axisPen = new Pen(Color.FromArgb(255, 123, 73), 2f);
        g.DrawLine(axisPen, Width * 0.14f, Height * 0.78f, Width * 0.88f, Height * 0.78f);

        using var barBrush = new LinearGradientBrush(
            new PointF(Width * 0.18f, Height * 0.35f),
            new PointF(Width * 0.18f, Height * 0.78f),
            Color.FromArgb(255, 140, 58),
            Color.FromArgb(244, 87, 40));
        g.FillRectangle(barBrush, Width * 0.2f, Height * 0.56f, Width * 0.07f, Height * 0.22f);
        g.FillRectangle(barBrush, Width * 0.31f, Height * 0.4f, Width * 0.08f, Height * 0.38f);
        g.FillRectangle(barBrush, Width * 0.43f, Height * 0.24f, Width * 0.08f, Height * 0.54f);

        using var sheetBrush = new SolidBrush(Color.FromArgb(245, 239, 243));
        g.FillRectangle(sheetBrush, Width * 0.54f, Height * 0.24f, Width * 0.29f, Height * 0.42f);

        using var gridPen = new Pen(Color.FromArgb(226, 214, 221), 1f);
        for (var i = 0; i < 4; i++)
        {
            g.DrawLine(gridPen, Width * 0.54f, Height * (0.24f + (i * 0.1f)), Width * 0.83f, Height * (0.24f + (i * 0.1f)));
        }

        for (var i = 0; i < 4; i++)
        {
            g.DrawLine(gridPen, Width * (0.54f + (i * 0.07f)), Height * 0.24f, Width * (0.54f + (i * 0.07f)), Height * 0.66f);
        }

        using var chartPen = new Pen(Color.FromArgb(247, 97, 45), 4f);
        chartPen.LineJoin = LineJoin.Round;
        g.DrawLines(chartPen,
        [
            new PointF(Width * 0.58f, Height * 0.55f),
            new PointF(Width * 0.66f, Height * 0.43f),
            new PointF(Width * 0.74f, Height * 0.47f),
            new PointF(Width * 0.84f, Height * 0.28f),
            new PointF(Width * 0.9f, Height * 0.38f)
        ]);

        using var gaugeBrush = new LinearGradientBrush(
            new PointF(Width * 0.72f, Height * 0.56f),
            new PointF(Width * 0.89f, Height * 0.9f),
            Color.FromArgb(255, 209, 145),
            Color.FromArgb(245, 104, 52));
        g.FillEllipse(gaugeBrush, Width * 0.74f, Height * 0.54f, Width * 0.2f, Width * 0.2f);
        using var gaugeInnerBrush = new SolidBrush(Color.FromArgb(245, 243, 244));
        g.FillEllipse(gaugeInnerBrush, Width * 0.785f, Height * 0.585f, Width * 0.11f, Width * 0.11f);
        using var needlePen = new Pen(Color.FromArgb(255, 116, 56), 4f);
        g.DrawLine(needlePen, Width * 0.84f, Height * 0.68f, Width * 0.88f, Height * 0.59f);
    }

    private static void DrawCorner(Graphics g, Pen pen, float x, float y, bool left, bool top)
    {
        const float length = 18f;

        g.DrawLine(pen, x, y, left ? x + length : x - length, y);
        g.DrawLine(pen, x, y, x, top ? y + length : y - length);
    }
}
