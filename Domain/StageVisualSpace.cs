namespace ReactionTimeMeasurementApp.Domain;

// A mérőfelület (stage) képpont-koordinátái és a látószög közötti átváltás.
//
// Feltevés: a résztvevő szeme a fixációs kereszttel szemben, a képernyő síkjára merőlegesen,
// ViewingDistanceMm távolságra van. A PixelsPerMm a kijelző VALÓDI pixelsűrűsége (a Tobii display
// area fizikai méretéből), nem a Windows logikai DPI-je.
public sealed class StageVisualSpace
{
    public StageVisualSpace(PointF fixationPoint, double pixelsPerMm, double viewingDistanceMm)
    {
        if (!(pixelsPerMm > 0) || !(viewingDistanceMm > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(pixelsPerMm), "A pixelsűrűségnek és a nézési távolságnak pozitívnak kell lennie.");
        }

        FixationPoint = fixationPoint;
        PixelsPerMm = pixelsPerMm;
        ViewingDistanceMm = viewingDistanceMm;
    }

    public PointF FixationPoint { get; }

    public double PixelsPerMm { get; }

    public double ViewingDistanceMm { get; }

    // Egy adott látószögű tárgy mérete képpontban, a látótengelyen (a fixációs pontnál) mérve.
    // Az excentrikus helyen megjelenő inger is ezt a méretet kapja (a PsychoPy "deg" egységéhez
    // hasonlóan). Ott ugyanaz a képpontméret kisebb szöget fed le: e excentricitásnál sugárirányban
    // cos²(e)-szer, érintőirányban cos(e)-szer akkorát (20 foknál kb. 12%, illetve 6% eltérés).
    public double SizeDegToPx(double sizeDeg)
    {
        return 2 * ViewingDistanceMm * Math.Tan(DegToRad(sizeDeg) / 2) * PixelsPerMm;
    }

    // Két képernyőpont közötti látószög (fok), a szemből nézve.
    public double AngleBetweenDeg(PointF a, PointF b)
    {
        var (ax, ay, az) = ToEyeVector(a);
        var (bx, by, bz) = ToEyeVector(b);
        var dot = (ax * bx) + (ay * by) + (az * bz);
        var lengths = Math.Sqrt((ax * ax) + (ay * ay) + (az * az)) * Math.Sqrt((bx * bx) + (by * by) + (bz * bz));
        return RadToDeg(Math.Acos(Math.Clamp(dot / lengths, -1, 1)));
    }

    public double EccentricityDeg(PointF point) => AngleBetweenDeg(point, FixationPoint);

    // A fixációs ponttól adott irányban (radián, 0 = jobbra, a képernyő-koordináták szerint) és
    // adott excentricitásban lévő képernyőpont.
    public PointF PointAtEccentricity(double eccentricityDeg, double directionRad)
    {
        var radiusPx = ViewingDistanceMm * Math.Tan(DegToRad(eccentricityDeg)) * PixelsPerMm;
        return new PointF(
            (float)(FixationPoint.X + (Math.Cos(directionRad) * radiusPx)),
            (float)(FixationPoint.Y + (Math.Sin(directionRad) * radiusPx)));
    }

    private (double X, double Y, double Z) ToEyeVector(PointF point)
    {
        return (
            (point.X - FixationPoint.X) / PixelsPerMm,
            (point.Y - FixationPoint.Y) / PixelsPerMm,
            ViewingDistanceMm);
    }

    private static double DegToRad(double degrees) => degrees * Math.PI / 180;

    private static double RadToDeg(double radians) => radians * 180 / Math.PI;
}
