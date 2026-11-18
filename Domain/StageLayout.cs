namespace ReactionTimeMeasurementApp.Domain;

// A mérőfelület (stage) állandó elrendezése. A mérés teljes képernyőn fut; minden érték a mérőfelület
// kliens-koordinátáiban, fizikai képpontban értendő (az alkalmazás PerMonitorV2 módban fut).
public static class StageLayout
{
    // Az inger és a fixációs kereszt legalább ennyire marad a mérőfelület szélétől és a
    // fotodióda-jelzőtől: a képernyő széle (keret, torzítás) és a jelző fénye ne zavarja az ingert, a
    // fotodióda pedig ne érzékelje az ingert.
    public const float SafeMarginPx = 36f;
}

// A mérőfelület méretéhez viszonyított pont: X a szélességhez, Y a magassághoz képest (0..1). Így a
// kijelölt hely a felbontás kisebb változása után is ugyanoda esik a képernyőn.
public readonly record struct NormalizedPoint(double X, double Y)
{
    public bool IsFinite => double.IsFinite(X) && double.IsFinite(Y);

    // Egész képpontból számolva, így a ToPixel pontosan ezt a képpontot adja vissza.
    public static NormalizedPoint FromPixel(Point pixel, Size stageSize) =>
        new(pixel.X / (double)stageSize.Width, pixel.Y / (double)stageSize.Height);

    public Point ToPixel(Size stageSize) => new(
        (int)Math.Round(stageSize.Width * X, MidpointRounding.AwayFromZero),
        (int)Math.Round(stageSize.Height * Y, MidpointRounding.AwayFromZero));
}

// A fotodióda-jelző egy kis négyzet a mérőfelületen, alapértelmezés szerint a jobb alsó sarokban; ha a
// fizikai fotodióda máshová került, a jelző a beállításokban áthelyezhető (HardwareSettings.
// PhotodiodeMarkerX/Y). A nézet az ingerrel EGYÜTT, ugyanabban a rajzolásban teszi ki: pontosan addig
// fehér, amíg az inger látszik. Egy erre szerelt fizikai fotodióda (az ESP-n keresztül) ebből méri,
// mikor jelent meg ténylegesen a kép.
//
// A négyzet körül egy GuardPx széles fekete védősáv van, az előtérben és a mérési jelenetben is,
// bármilyen háttérnél: így világos háttér fénye sem jut a négyzet köré tett érzékelőre, és az ESP
// küszöbe mindig ugyanazt a fekete -> fehér átmenetet látja. Csak a belső négyzet vált fehérre.
public static class PhotodiodeMarker
{
    public const int SizePx = 56;

    // Az alapértelmezett helyen a négyzet ennyire van a jobb és az alsó széltől.
    public const int MarginPx = 20;

    // A védősáv kisebb, mint a StageLayout.SafeMarginPx (az inger és a kereszt elhelyezését nem
    // befolyásolja). Az alapértelmezett helyen a mérőfelületen belül marad (kisebb, mint a MarginPx); a
    // képernyő széléhez tolt négyzetnél a szél felé eső része kilóg, és egyszerűen nem rajzolódik ki (ott
    // nincs kép, így fény sem jut a szenzorra).
    public const int GuardPx = 16;

    // Az érzékelt négyzet. center: a négyzet középpontja a mérőfelülethez viszonyítva, vagy null az
    // alapértelmezett helyhez (jobb alsó sarok, a széltől MarginPx-re). A beállított hely a képernyő
    // legszéléig engedett: a négyzet csak a mérőfelületen belül marad (a kilógó helyet a szélre
    // igazítja). Üres, ha a mérőfelület túl kicsi hozzá.
    public static Rectangle GetBounds(Size stageSize, NormalizedPoint? center)
    {
        if (center is not { IsFinite: true } point)
        {
            var defaultX = stageSize.Width - SizePx - MarginPx;
            var defaultY = stageSize.Height - SizePx - MarginPx;
            return defaultX < 0 || defaultY < 0 ? Rectangle.Empty : new Rectangle(defaultX, defaultY, SizePx, SizePx);
        }

        var maxX = stageSize.Width - SizePx;
        var maxY = stageSize.Height - SizePx;
        if (maxX < 0 || maxY < 0)
        {
            return Rectangle.Empty;
        }

        var pixel = point.ToPixel(stageSize);
        var x = Math.Clamp(pixel.X - (SizePx / 2), 0, maxX);
        var y = Math.Clamp(pixel.Y - (SizePx / 2), 0, maxY);
        return new Rectangle(x, y, SizePx, SizePx);
    }

    // A négyzet középpontja (a beállításokba ez kerül, lásd NormalizedPoint.FromPixel).
    public static Point GetCenter(Rectangle bounds) => new(bounds.X + (SizePx / 2), bounds.Y + (SizePx / 2));

    // A négyzet a védősávval együtt (mindig fekete, kivéve a belső négyzetet). Üres, ha a négyzet is az.
    public static Rectangle GetGuardBounds(Rectangle bounds)
    {
        return bounds.IsEmpty ? Rectangle.Empty : Rectangle.Inflate(bounds, GuardPx, GuardPx);
    }
}

// A fixációs kereszt mérete. A kereszt bármilyen háttéren látszik: két, lapos végű (flat cap) menetben
// rajzolódik, előbb fekete (ThicknessPx + 2 * OutlinePx vastag, a szárak OutlinePx-szel hosszabbak),
// majd fölé fehér (ThicknessPx vastag). A középpontja a fixációs pont.
public static class FixationCross
{
    // Egy szár (vízszintes, illetve függőleges vonal) teljes hossza; a középponttól mindkét irányban a fele.
    public const float ArmLengthPx = 20f;

    // A fehér vonal vastagsága.
    public const float ThicknessPx = 3f;

    // A fekete körvonal szélessége a fehér vonal körül.
    public const float OutlinePx = 2f;

    // A kereszt körülíró körének sugara: a fekete menet sarka a középponttól
    // hypot(ArmLengthPx / 2 + OutlinePx, ThicknessPx / 2 + OutlinePx) = hypot(12, 3,5) = 12,5 képpontra van,
    // felfelé kerekítve 13, plusz 1 képpont az élsimításnak: 14. Az inger nem lóghat bele ebbe a körbe
    // (ExperimentSettings.Validate), a kör pedig a mérőfelület biztonsági margóján belül marad
    // (StimulusPlacement.IsFixationPointAllowed).
    public const float BoundingRadiusPx = 14f;
}
