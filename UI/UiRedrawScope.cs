using System.Runtime.InteropServices;

namespace ReactionTimeMeasurementApp.UI;

// Egy vezérlő (és gyermekei) újrarajzolásának felfüggesztése: a köztes állapotok nem rajzolódnak ki, a
// Dispose pedig egyetlen szinkron újrarajzolással (háttérrel és kerettel együtt) teljesen kirajzolja az
// új állapotot, mielőtt visszatérne.
internal sealed class UiRedrawScope : IDisposable
{
    private const int WM_SETREDRAW = 0x000B;

    private readonly Control _control;
    private readonly bool _hadHandle;
    private bool _disposed;

    private UiRedrawScope(Control control)
    {
        _control = control;
        _hadHandle = control.IsHandleCreated;

        if (_hadHandle)
        {
            _ = SendMessage(control.Handle, WM_SETREDRAW, 0, 0);
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

        _ = SendMessage(_control.Handle, WM_SETREDRAW, 1, 0);
        ViewTransition.RedrawNow(_control.Handle);
    }

    [DllImport("user32.dll")]
    private static extern nint SendMessage(nint hWnd, int msg, nint wParam, nint lParam);
}
