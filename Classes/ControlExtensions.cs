using System.Reflection;

namespace ReactionTimeMeasurementApp.Classes;

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
}
