using ReactionTimeMeasurementApp.Diagnostics;
using ReactionTimeMeasurementApp.Domain;

namespace ReactionTimeMeasurementApp.Measurement;

// A mérés geometriája: a mérőfelület, a fixációs pont, az ingerhelyek tartománya és a képidőzítés a mérés
// elején egyszer rögzül, és a metaadatok közé kerül. A Tobii display area (a kijelző fizikai mérete)
// csak akkor kell, ha van szemkövető: tekintet-ellenőrzésnél kötelező (ez vetíti a tekintetet a
// képernyőre), anélkül csak figyelmeztetés, ha hiányzik vagy nem illik a kijelzőhöz.
internal static class SessionGeometryBuilder
{
    // Ha a Tobii display area méretaránya ennél jobban eltér a kijelzőétől, a display area
    // valószínűleg egy másik monitorra van beállítva - ilyenkor a tekintet sem a valódi helyére vetül.
    private const double MaxDisplayAspectMismatch = 0.05;

    // MeasurementSetupException-t dob, ha a mérés ezen a mérőfelületen (vagy ezzel a display areával) nem
    // indítható.
    public static async Task<SessionGeometry> CaptureAsync(
        StageInfo stage,
        ExperimentSettings settings,
        IGazeValidationRule rule,
        bool gazeActive,
        IDisplayTimingProvider displayTimingProvider,
        IGazeTracker gazeTracker)
    {
        var feasibility = StageFeasibility.Evaluate(settings, stage.StageSize);
        if (!feasibility.IsFeasible)
        {
            throw new MeasurementSetupException(string.Join(Environment.NewLine, feasibility.Errors));
        }

        foreach (var warning in feasibility.Warnings)
        {
            Logger.Warning(warning.Message);
        }

        var effectiveMaxEccentricity = feasibility.EffectiveMaxEccentricityPx
            ?? throw new InvalidOperationException("Az inger legnagyobb elférő távolsága nincs meghatározva.");
        var placement = new StimulusPlacement(
            stage.StageSize, feasibility.PhotodiodeBounds, feasibility.FixationPoint, settings.Stimulus.DiameterPx);

        var bounds = stage.ScreenBounds;
        var timing = displayTimingProvider.Read(stage.ScreenDeviceName, bounds);
        if (timing.Warning is not null)
        {
            Logger.Warning($"[SCANOUT] {timing.Warning}");
        }

        PhysicalDisplaySize? displaySize = null;
        string? displayAreaWarning = null;
        if (gazeActive)
        {
            displaySize = await gazeTracker.RefreshDisplayAreaAsync();
            displayAreaWarning = CheckDisplayArea(displaySize, bounds);
            if (displayAreaWarning is not null)
            {
                if (rule.RequiresTracker)
                {
                    throw new MeasurementSetupException(
                        $"{displayAreaWarning} Ellenőrizd a display area beállítását a Tobii Pro Eye Tracker Managerben, majd próbáld újra.");
                }

                Logger.Warning(
                    $"{displayAreaWarning} A mérés tekintet-ellenőrzés nélkül fut, ezért elindul; a rögzített tekintetadat "
                    + "képernyőhelye nem megbízható.");
            }
        }

        var displaySizeText = displaySize is null
            ? "fizikai méret ismeretlen"
            : $"{displaySize.WidthMm:0.0}x{displaySize.HeightMm:0.0} mm";
        Logger.Info(
            $"Kijelző: {stage.ScreenDeviceName} {bounds.Width}x{bounds.Height} px, {displaySizeText}, {timing.RefreshRateHz:0.###} Hz ({timing.Source}); "
            + $"mérőfelület {stage.StageSize.Width}x{stage.StageSize.Height} px, fixációs pont ({feasibility.FixationPoint.X:0}, {feasibility.FixationPoint.Y:0}), "
            + $"ingertávolság {settings.Stimulus.MinEccentricityPx}–{effectiveMaxEccentricity:0} px.");

        return new SessionGeometry(
            stage,
            feasibility.FixationPoint,
            placement,
            effectiveMaxEccentricity,
            timing,
            displaySize,
            displayAreaWarning);
    }

    // Null, ha a display area rendben van; különben a probléma leírása (ez kerül a metaadatok közé is).
    private static string? CheckDisplayArea(PhysicalDisplaySize? displaySize, Rectangle screenBounds)
    {
        if (displaySize is null)
        {
            return "A Tobii szemkövetőn nincs beállítva a kijelző (display area), pedig ez vetíti a tekintetet a képernyőre: "
                + "enélkül nem dönthető el, hová nézett a résztvevő.";
        }

        var screenAspect = screenBounds.Width / (double)screenBounds.Height;
        var displayAspect = displaySize.WidthMm / displaySize.HeightMm;
        if (Math.Abs((screenAspect / displayAspect) - 1) <= MaxDisplayAspectMismatch)
        {
            return null;
        }

        return $"A Tobii display area ({displaySize.WidthMm:0} × {displaySize.HeightMm:0} mm) méretaránya nem egyezik a mérést futtató "
            + $"kijelzőével ({screenBounds.Width} × {screenBounds.Height} képpont), így a tekintet nem a valódi helyére vetülne a képernyőn. "
            + "Valószínűleg a display area egy másik monitorra van beállítva, vagy az alkalmazás nem azon a monitoron fut, amelyikre a "
            + "szemkövető be van állítva.";
    }
}
