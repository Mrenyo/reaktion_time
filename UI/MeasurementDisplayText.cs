using System.Globalization;
using ReactionTimeMeasurementApp.Domain;

namespace ReactionTimeMeasurementApp.UI;

// A mérőfelület helyeinek felületen megjelenő szövegei (a mérési eredményekéi: Measurement.ResultDisplayText).
internal static class MeasurementDisplayText
{
    // Egy pont a mérőfelületen (a beállítási ablakban és a kijelölők szalagján), pl.
    // "x = 640, y = 360 px (33,3% · 33,3%)".
    internal static string FormatStagePoint(Point pixel, Size stageSize)
    {
        var culture = CultureInfo.CurrentCulture;
        var percentX = stageSize.Width > 0 ? pixel.X * 100d / stageSize.Width : 0;
        var percentY = stageSize.Height > 0 ? pixel.Y * 100d / stageSize.Height : 0;
        return string.Format(
            culture,
            "x = {0}, y = {1} px ({2}% · {3}%)",
            pixel.X,
            pixel.Y,
            percentX.ToString("0.0", culture),
            percentY.ToString("0.0", culture));
    }

    // A fotodióda-jelző helye (a négyzet középpontja), pl.
    // "jobb alsó sarok (alapértelmezett) · közép: x = 1872, y = 1032 px (97,5% · 95,6%)".
    internal static string FormatPhotodiodePosition(Rectangle bounds, Size stageSize, bool isDefault)
    {
        if (bounds.IsEmpty)
        {
            return "a képernyő túl kicsi a jelzőhöz";
        }

        var text = "közép: " + FormatStagePoint(PhotodiodeMarker.GetCenter(bounds), stageSize);
        return isDefault ? "jobb alsó sarok (alapértelmezett) · " + text : text;
    }
}
