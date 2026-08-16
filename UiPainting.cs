using System.Runtime.InteropServices;

namespace ReactionTimeMeasurementApp;

internal sealed class BufferedPanel : Panel
{
    private const int WS_CLIPCHILDREN = 0x02000000;
    private const int WS_CLIPSIBLINGS = 0x04000000;

    public BufferedPanel()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.UserPaint,
            true);

        UpdateStyles();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var createParams = base.CreateParams;
            createParams.Style |= WS_CLIPCHILDREN | WS_CLIPSIBLINGS;
            return createParams;
        }
    }
}

internal sealed class TransitionCover : Control
{
    private static readonly Color FallbackColor = Color.FromArgb(253, 250, 250);

    public TransitionCover()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.UserPaint,
            true);

        BackColor = FallbackColor;
        TabStop = false;
        UpdateStyles();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var background = ViewBackgroundStyler.TryGetScaledBackground(ClientSize);
        if (background is not null)
        {
            e.Graphics.DrawImageUnscaled(background, 0, 0);
            return;
        }

        using var brush = new SolidBrush(FallbackColor);
        e.Graphics.FillRectangle(brush, ClientRectangle);
    }
}
internal sealed class UiRedrawScope : IDisposable
{
    private const int WM_SETREDRAW = 0x000B;
    private const int RDW_INVALIDATE = 0x0001;
    private const int RDW_ALLCHILDREN = 0x0080;
    private const int RDW_UPDATENOW = 0x0100;

    private readonly Control _control;
    private readonly bool _hadHandle;
    private bool _disposed;

    private UiRedrawScope(Control control)
    {
        _control = control;
        _hadHandle = control.IsHandleCreated;

        if (_hadHandle)
        {
            SendMessage(control.Handle, WM_SETREDRAW, false, 0);
        }
    }

    public static UiRedrawScope Suspend(Control control) => new(control);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (!_hadHandle || _control.IsDisposed)
        {
            return;
        }

        SendMessage(_control.Handle, WM_SETREDRAW, true, 0);
        RedrawWindow(_control.Handle, IntPtr.Zero, IntPtr.Zero, RDW_INVALIDATE | RDW_ALLCHILDREN | RDW_UPDATENOW);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, bool wParam, int lParam);

    [DllImport("user32.dll")]
    private static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, int flags);
}

