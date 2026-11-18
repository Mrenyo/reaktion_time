using System.Drawing.Drawing2D;
using System.Reflection;

namespace ReactionTimeMeasurementApp.UI;

internal static class ControlExtensions
{
    // A Control.DoubleBuffered védett ("protected") tag, a WinForms nem ad publikus API-t a kívülről
    // bekapcsolására. Ez a reflection a szokásos megoldás villódzásmentes újrarajzoláshoz olyan
    // vezérlőkön (pl. Panel), amiket nem mi származtatunk.
    private static readonly PropertyInfo? DoubleBufferedProperty =
        typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);

    // A vezérlőn belüli összes panel (Panel, TableLayoutPanel, ...) és képdoboz kettős pufferelése. Az
    // átlátszó hátterű, a nézetet kitöltő elrendező panelek a szülő hátterét (a teljes háttérképet) is
    // maguk alá rajzolják; puffer nélkül ez közvetlenül a képernyőre, GDI+-szal történne, ami nagyon lassú
    // (egy teljes képernyőnyi háttér több tíz ms), és látszana, ahogy az elemek egymás után kirajzolódnak.
    internal static void EnableDoubleBufferingForPanels(this Control root)
    {
        foreach (Control child in root.Controls)
        {
            if (child is Panel or PictureBox)
            {
                DoubleBufferedProperty?.SetValue(child, true);
            }

            child.EnableDoubleBufferingForPanels();
        }
    }

    // Lekerekített sarkú kivágást (Region) állít be a vezérlőn, és minden méretváltozáskor frissíti. A
    // sugár logikai (96 DPI-s) képpontban értendő: a vezérlő DPI-jére váltjuk át, így a vezérlő
    // méretével együtt nő.
    internal static void KeepRoundedCorners(this Control control, int logicalRadius)
    {
        ApplyRoundedRegion(control, logicalRadius);
        control.Resize += (_, _) => ApplyRoundedRegion(control, logicalRadius);
    }

    private static void ApplyRoundedRegion(Control control, int logicalRadius)
    {
        if (control.Width <= 0 || control.Height <= 0)
        {
            return;
        }

        using var path = new GraphicsPath();
        var diameter = Math.Min(control.LogicalToDeviceUnits(logicalRadius) * 2, Math.Min(control.Width, control.Height));
        var rect = new Rectangle(0, 0, control.Width, control.Height);

        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();

        var previousRegion = control.Region;
        control.Region = new Region(path);
        previousRegion?.Dispose();
    }
}
