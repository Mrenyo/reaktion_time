using System.Drawing.Drawing2D;
using System.Reflection;

namespace ReactionTimeMeasurementApp.UI;

internal static class ControlExtensions
{
    private static readonly PropertyInfo? DoubleBufferedProperty =
        typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);

    // A Control.DoubleBuffered védett ("protected") tagja, ezért egy másik vezérlő (pl. a MainMenu
    // Form) kívülről nem tudja bekapcsolni rajta - a WinForms erre nem ad publikus API-t. Ez a
    // reflection a szokásos megoldás villódzásmentes újrarajzoláshoz olyan vezérlőkön (pl. Panel),
    // amiket nem mi származtatunk.
    internal static void EnableDoubleBuffering(this Control control)
    {
        DoubleBufferedProperty?.SetValue(control, true);
    }

    // Lekerekített sarkú kivágást (Region) állít be a vezérlőn, és minden méretváltozáskor frissíti.
    internal static void KeepRoundedCorners(this Control control, int radius)
    {
        ApplyRoundedRegion(control, radius);
        control.Resize += (_, _) => ApplyRoundedRegion(control, radius);
    }

    private static void ApplyRoundedRegion(Control control, int radius)
    {
        if (control.Width <= 0 || control.Height <= 0)
        {
            return;
        }

        using var path = new GraphicsPath();
        var diameter = radius * 2;
        var rect = new Rectangle(0, 0, control.Width - 1, control.Height - 1);

        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();

        control.Region?.Dispose();
        control.Region = new Region(path);
    }
}
