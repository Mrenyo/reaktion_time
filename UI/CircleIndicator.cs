using System.Drawing.Drawing2D;

namespace ReactionTimeMeasurementApp.UI;

public class CircleIndicator : Control
{
    private const int WmNchittest = 0x84;
    private const int HtTransparent = -1;
    private const float OutlineThickness = 2f;

    public CircleIndicator()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.UserPaint |
            ControlStyles.SupportsTransparentBackColor,
            true);

        BackColor = Color.Transparent;
        ForeColor = Color.FromArgb(138, 138, 138);
        Size = new Size(40, 40);
        TabStop = false;
        UpdateRingRegion();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateRingRegion();
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        // Intentionally empty: the control itself has no background, only an outline ring.
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        var inset = OutlineThickness / 2f;
        var width = Width - OutlineThickness - 1f;
        var height = Height - OutlineThickness - 1f;
        if (width <= 0f || height <= 0f)
        {
            return;
        }

        using var pen = new Pen(ForeColor, OutlineThickness);
        e.Graphics.DrawEllipse(pen, inset, inset, width, height);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmNchittest)
        {
            m.Result = (IntPtr)HtTransparent;
            return;
        }

        base.WndProc(ref m);
    }

    private void UpdateRingRegion()
    {
        if (Width <= 2 || Height <= 2)
        {
            return;
        }

        using var path = new GraphicsPath(FillMode.Alternate);
        var outer = new RectangleF(0, 0, Width - 1f, Height - 1f);
        var innerInset = Math.Max(1f, OutlineThickness);
        var inner = RectangleF.Inflate(outer, -innerInset, -innerInset);

        path.AddEllipse(outer);
        if (inner.Width > 0f && inner.Height > 0f)
        {
            path.AddEllipse(inner);
        }

        Region?.Dispose();
        Region = new Region(path);
    }
}
