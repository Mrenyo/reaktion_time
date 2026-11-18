namespace ReactionTimeMeasurementApp.UI;

// A felület több helyen is használt színei egy helyen. (A Designer-fájlok a tervező miatt a saját
// literáljaikat használják; azoknak ugyanezek az értékek felelnek meg.)
internal static class UiTheme
{
    // A kiemelő narancs: címek, elsődleges gombok, táblázatfejléc.
    internal static readonly Color Accent = Color.FromArgb(255, 91, 24);

    // A letiltott elsődleges gomb halványabb narancsa.
    internal static readonly Color AccentDisabled = Color.FromArgb(244, 196, 176);

    // A nézetek háttere, ha a háttérkép nem érhető el.
    internal static readonly Color PageBackground = Color.FromArgb(248, 244, 243);

    // A kártyák és a tartalom háttere.
    internal static readonly Color CardBackground = Color.FromArgb(253, 250, 250);

    // A mérőfelület háttere, ha a beállított háttérszín nem értelmezhető (megegyezik az alapértelmezett
    // "#1E1E1E" háttérszínnel).
    internal static readonly Color FallbackStageBackground = Color.FromArgb(30, 30, 30);

    // A kártyák és a gombok lekerekítésének sugara logikai (96 DPI-s) képpontban; a KeepRoundedCorners
    // váltja át a vezérlő DPI-jére.
    internal const int CardCornerRadius = 28;
    internal const int ButtonCornerRadius = 16;

    // Halvány (másodlagos vagy gyakorló) szöveg.
    internal static readonly Color MutedText = Color.FromArgb(150, 150, 156);
}
