namespace ReactionTimeMeasurementApp.Domain;

// Az inger elhelyezése a mérőfelületen, tisztán képpont-geometriával (a mérőfelület koordinátáiban).
// Ugyanezt használja a mérés, a beállítási ablak előnézete és a fixációs pont kijelölője.
//
// Az inger (DiameterPx átmérőjű kör) középpontja a fixációs ponttól r távolságra, θ irányban van
// (θ = 0: jobbra, a képernyő-koordináták szerint, vagyis pozitív szögnél lefelé fordulva). Egy hely
// akkor megengedett, ha az inger befoglaló négyzete
//   - a mérőfelületen belül, a széltől legalább StageLayout.SafeMarginPx távolságra marad, és
//   - nem metszi a fotodióda-jelzőt SafeMarginPx-szel megnövelve (a fotodióda nem érzékelheti az ingert).
public sealed class StimulusPlacement
{
    // Az irányok vizsgálatának felbontása: 3600 irány, 0,1 fokonként.
    private const int DirectionCount = 3600;

    // Egy kisorsolt távolsághoz legfeljebb ennyi véletlen irányt próbál ki a mintavétel, mielőtt
    // végigpásztázná az összes irányt.
    private const int MaxRandomDirectionAttempts = 360;

    // Ennyiszer sorsol új távolságot, ha egy távolsághoz egyetlen irány sem fér el (a gyakorlatban nem
    // fordul elő, mert a FindEffectiveMaxEccentricity csak elférő távolságokat enged).
    private const int MaxDistanceAttempts = 100;

    // A legnagyobb elférő távolság bináris keresésének pontossága.
    private const double DistanceSearchTolerancePx = 0.5;

    private readonly RectangleF _allowedArea;
    private readonly RectangleF _photodiodeKeepOut;

    // photodiodeBounds: a fotodióda-jelző négyzete (HardwareSettings.ResolvePhotodiodeBounds); üres, ha
    // nincs jelző.
    public StimulusPlacement(Size stageSize, Rectangle photodiodeBounds, PointF fixationPoint, float diameterPx)
    {
        StageSize = stageSize;
        FixationPoint = fixationPoint;
        DiameterPx = diameterPx;
        _allowedArea = GetAllowedArea(stageSize);
        _photodiodeKeepOut = GetPhotodiodeKeepOut(photodiodeBounds);
    }

    public Size StageSize { get; }

    public PointF FixationPoint { get; }

    public float DiameterPx { get; }

    // Az inger befoglaló négyzete adott távolságban és irányban (radián); hamis, ha ott nem fér el.
    public bool TryCreateStimulusRect(double eccentricityPx, double directionRad, out RectangleF rect)
    {
        var radius = DiameterPx / 2d;
        var centerX = FixationPoint.X + (Math.Cos(directionRad) * eccentricityPx);
        var centerY = FixationPoint.Y + (Math.Sin(directionRad) * eccentricityPx);
        rect = new RectangleF((float)(centerX - radius), (float)(centerY - radius), DiameterPx, DiameterPx);

        if (!_allowedArea.Contains(rect))
        {
            return false;
        }

        return _photodiodeKeepOut.IsEmpty || !rect.IntersectsWith(_photodiodeKeepOut);
    }

    // Elfér-e az inger az adott távolságban legalább egy irányban (0,1 fokos lépésekkel vizsgálva).
    public bool CanPlaceAt(double eccentricityPx) => FindFirstPlacement(eccentricityPx) is not null;

    // Az inger helye az adott távolságban, az első elférő irányban (jobbról indulva, 0,1 fokos
    // lépésekkel); null, ha egyik irányban sem fér el. A beállítási ablak előnézete ezzel mutat mintaingert.
    public RectangleF? FindFirstPlacement(double eccentricityPx)
    {
        for (var step = 0; step < DirectionCount; step++)
        {
            if (TryCreateStimulusRect(eccentricityPx, StepToRadians(step), out var rect))
            {
                return rect;
            }
        }

        return null;
    }

    // A [minPx, maxPx] tartomány legnagyobb, a mérőfelületen még elférő távolsága, egész képpontra
    // lefelé kerekítve; null, ha már a legkisebb távolság sem fér el. A legtávolabbi saroknál
    // messzebb semmi nem fér el, ezért a keresés felső határa legfeljebb ez.
    //
    // A bináris keresés azért helyes, mert az ingerközéppont megengedett tartománya összefüggő (a
    // margóval szűkített téglalap, amelyből a fotodióda-jelző egy téglalapot kivág), így a fixációs
    // ponttól mért távolság ezen a tartományon minden köztes értéket felvesz: az elférő távolságok
    // egyetlen intervallumot alkotnak. Hogy a kivágás nem vágja-e ketté a tartományt, az
    // IsCenterAreaSplit ellenőrzi (a StageFeasibility ilyenkor hibát ad).
    public double? FindEffectiveMaxEccentricity(double minPx, double maxPx)
    {
        if (!CanPlaceAt(minPx))
        {
            return null;
        }

        var upper = Math.Min(maxPx, GetFarthestCornerDistance());
        if (upper <= minPx)
        {
            return minPx;
        }

        if (CanPlaceAt(upper))
        {
            return Math.Max(minPx, Math.Floor(upper));
        }

        var placeable = minPx;
        var notPlaceable = upper;
        while (notPlaceable - placeable > DistanceSearchTolerancePx)
        {
            var middle = (placeable + notPlaceable) / 2;
            if (CanPlaceAt(middle))
            {
                placeable = middle;
            }
            else
            {
                notPlaceable = middle;
            }
        }

        // A határ a (placeable, notPlaceable) szakaszba esik, amely legfeljebb fél képpont hosszú, így
        // legfeljebb egy egész érték lehet benne: ezt még kipróbálja, hogy az eredmény a legnagyobb elférő
        // egész távolság legyen.
        var result = Math.Floor(placeable);
        if (result + 1 < notPlaceable && CanPlaceAt(result + 1))
        {
            result++;
        }

        return Math.Max(minPx, result);
    }

    // Kétlépcsős mintavétel, hogy a TÁVOLSÁG eloszlása egyenletes legyen [minPx, effectiveMaxPx]
    // között:
    //   1. az r távolság egyenletes eloszlásból [minPx, effectiveMaxPx] között, egyszer sorsolva;
    //   2. a θ irány egyenletes eloszlásból [0, 2π) között; ha ott nem fér el az inger, csak az irányt
    //      sorsolja újra (legfeljebb MaxRandomDirectionAttempts-szer), majd ha ez sem sikerül, 0,1
    //      fokonként végigpásztázza az irányokat, és az elférők közül egyenletesen választ egyet.
    // Új távolságot csak akkor sorsol, ha az adott r-nél egyetlen irány sem fér el (legfeljebb
    // MaxDistanceAttempts-szer), utána hamisat ad. Az eredmény: a távolság egyenletes eloszlású, az
    // irány pedig egyenletes az adott távolságban a képernyőn elférő irányok között. (Ha a távolságot
    // és az irányt minden elutasítás után együtt sorsolnánk újra, a nagy távolságok - ahol kevesebb
    // irány fér el - alulreprezentáltak lennének.)
    public bool TrySampleStimulusRect(Random random, double minPx, double effectiveMaxPx, out RectangleF rect)
    {
        for (var distanceAttempt = 0; distanceAttempt < MaxDistanceAttempts; distanceAttempt++)
        {
            var eccentricityPx = effectiveMaxPx > minPx
                ? minPx + (random.NextDouble() * (effectiveMaxPx - minPx))
                : minPx;

            for (var directionAttempt = 0; directionAttempt < MaxRandomDirectionAttempts; directionAttempt++)
            {
                if (TryCreateStimulusRect(eccentricityPx, random.NextDouble() * 2 * Math.PI, out rect))
                {
                    return true;
                }
            }

            var fittingSteps = new List<int>();
            for (var step = 0; step < DirectionCount; step++)
            {
                if (TryCreateStimulusRect(eccentricityPx, StepToRadians(step), out _))
                {
                    fittingSteps.Add(step);
                }
            }

            if (fittingSteps.Count > 0
                && TryCreateStimulusRect(eccentricityPx, StepToRadians(fittingSteps[random.Next(fittingSteps.Count)]), out rect))
            {
                return true;
            }
        }

        rect = RectangleF.Empty;
        return false;
    }

    // Kettévágja-e a fotodióda-jelző tiltott környéke az ingerközéppont megengedett tartományát. A
    // tartomány a margóval és az inger sugarával szűkített téglalap, amelyből a jelző (SafeMarginPx-szel
    // és az inger sugarával megnövelt) téglalapja kivág egy részt. Ez akkor vágja ketté, ha az egyik
    // irányban teljesen átér rajta, a másikban viszont mindkét oldalán marad hely - ilyenkor a jelző a
    // képernyő belsejében van, és az elférő távolságok nem feltétlenül alkotnak egyetlen intervallumot.
    public bool IsCenterAreaSplit()
    {
        if (_photodiodeKeepOut.IsEmpty)
        {
            return false;
        }

        var radius = DiameterPx / 2f;
        var centers = RectangleF.Inflate(_allowedArea, -radius, -radius);
        if (centers.Width < 0 || centers.Height < 0)
        {
            return false;
        }

        var hole = RectangleF.Inflate(_photodiodeKeepOut, radius, radius);
        var spansVertically = hole.Top <= centers.Top && hole.Bottom >= centers.Bottom;
        var spansHorizontally = hole.Left <= centers.Left && hole.Right >= centers.Right;
        var leavesBothSidesX = hole.Left > centers.Left && hole.Right < centers.Right;
        var leavesBothSidesY = hole.Top > centers.Top && hole.Bottom < centers.Bottom;
        return (spansVertically && leavesBothSidesX) || (spansHorizontally && leavesBothSidesY);
    }

    // A fixációs kereszt (FixationCross.BoundingRadiusPx sugarú kör befoglaló négyzete) a mérőfelület
    // biztonsági margóján belül van, és nem metszi a SafeMarginPx-szel megnövelt fotodióda-jelzőt
    // (photodiodeBounds; üres, ha nincs jelző). A tiltott sávok így pontosan a kijelölőn jelölt téglalapok.
    public static bool IsFixationPointAllowed(Size stageSize, Rectangle photodiodeBounds, PointF point)
    {
        if (!float.IsFinite(point.X) || !float.IsFinite(point.Y))
        {
            return false;
        }

        var radius = FixationCross.BoundingRadiusPx;
        var crossBounds = new RectangleF(point.X - radius, point.Y - radius, 2 * radius, 2 * radius);
        if (!GetAllowedArea(stageSize).Contains(crossBounds))
        {
            return false;
        }

        var keepOut = GetPhotodiodeKeepOut(photodiodeBounds);
        return keepOut.IsEmpty || !crossBounds.IntersectsWith(keepOut);
    }

    public static PointF GetCenter(RectangleF bounds) => new(bounds.X + (bounds.Width / 2f), bounds.Y + (bounds.Height / 2f));

    public static double Distance(PointF a, PointF b)
    {
        var dx = (double)a.X - b.X;
        var dy = (double)a.Y - b.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    private static double StepToRadians(int step) => step * 2 * Math.PI / DirectionCount;

    private static RectangleF GetAllowedArea(Size stageSize)
    {
        var margin = StageLayout.SafeMarginPx;
        return new RectangleF(margin, margin, stageSize.Width - (2 * margin), stageSize.Height - (2 * margin));
    }

    private static RectangleF GetPhotodiodeKeepOut(Rectangle photodiodeBounds)
    {
        if (photodiodeBounds.IsEmpty)
        {
            return RectangleF.Empty;
        }

        var keepOut = (RectangleF)photodiodeBounds;
        keepOut.Inflate(StageLayout.SafeMarginPx, StageLayout.SafeMarginPx);
        return keepOut;
    }

    private double GetFarthestCornerDistance()
    {
        var width = StageSize.Width;
        var height = StageSize.Height;
        return new[]
        {
            Distance(FixationPoint, new PointF(0, 0)),
            Distance(FixationPoint, new PointF(width, 0)),
            Distance(FixationPoint, new PointF(0, height)),
            Distance(FixationPoint, new PointF(width, height))
        }.Max();
    }
}
