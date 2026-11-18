using ReactionTimeMeasurementApp.Domain;

namespace ReactionTimeMeasurementApp.Measurement;

// Az előző mérés kimenetele az előtér állapotsorához.
//   MissingEspTrials  - mért próbák, amelyekben volt kattintás, de nincs ESP-reakcióidő.
//   NoGazeDataTrials  - próbák, amelyekben egyetlen tekintetminta sem érkezett (a szemkövető kiesett).
internal sealed record LastSessionSummary(
    SessionStatus Status,
    int ValidTrials,
    int PlannedTrials,
    double? EspMedianMs,
    int MissingEspTrials,
    int NoGazeDataTrials);

// Az előtér szövegei: a résztvevőnek szóló instrukció, a kutatónak szóló összefoglaló a beállításokról
// és az előző mérés kimenetele. Tiszta függvények: csak a paramétereikből dolgoznak.
internal static class LobbyText
{
    private static readonly char[] LineSeparators = { '\r', '\n' };

    // Hibás beállításoknál (rule == null) nincs instrukció: a mérés úgysem indulhat el, a hibát az
    // összefoglaló mutatja.
    public static string BuildInstruction(ExperimentSettings settings, IGazeValidationRule? rule)
    {
        if (rule is null)
        {
            return string.Empty;
        }

        var text = rule.InstructionText;
        if (settings.Gaze.EffectivePlacement == FixationPlacement.Custom)
        {
            text += " A kereszt a képernyő egy kijelölt pontján lesz.";
        }

        if (settings.Trials.ResponseMode == ResponseMode.ClickOnStimulus)
        {
            text += " Az ingerre kell kattintani: ha mellé kattintasz, a próba érvénytelen.";
        }

        var practiceTrials = settings.Trials.PracticeTrials;
        if (practiceTrials > 0)
        {
            text += $" Először {practiceTrials} gyakorló inger következik, ezek nem számítanak bele az eredménybe.";
        }

        return text + " (Megszakítás: Esc)";
    }

    public static string BuildSettingsSummary(ExperimentSettings settings, string? error)
    {
        if (error is not null)
        {
            var firstError = error.Split(LineSeparators, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? error;
            return "A beállítások hibásak – javítsd őket a Testreszabásban: " + firstError;
        }

        var trials = settings.Trials;
        var stimulus = settings.Stimulus;
        var gaze = settings.Gaze;

        var trialsText = trials.PracticeTrials > 0
            ? $"{trials.MeasuredTrials} mért + {trials.PracticeTrials} gyakorló inger"
            : $"{trials.MeasuredTrials} mért inger (gyakorlás nélkül)";
        var stimulusText =
            $"inger: {stimulus.DiameterPx} px, {FormatColor(stimulus.Color)} a {FormatColor(stimulus.BackgroundColor)} háttéren";
        var gazeText = gaze.Validation switch
        {
            GazeValidationMode.LookAtStimulus => $"tekintet: az ingert kell nézni (elfogadási rádiusz: {gaze.ValidityRadiusPx} px)",
            GazeValidationMode.KeepFixation =>
                $"tekintet: a fixációs pontot kell nézni ({(gaze.EffectivePlacement == FixationPlacement.Custom ? "egyéni helyen" : "középen")}), "
                + $"elfogadási rádiusz: {gaze.ValidityRadiusPx} px",
            _ => "tekintet: nincs ellenőrzés"
        };

        var responseText = trials.ResponseMode == ResponseMode.ClickOnStimulus
            ? "válasz: kattintás az ingerre"
            : "válasz: kattintás bárhol";

        return string.Join(" · ", trialsText, responseText, stimulusText, gazeText, $"válaszidő-korlát: {trials.ReactionTimeoutMs} ms");

        static string FormatColor(string text) => HexColor.TryParse(text, out var color) ? HexColor.Format(color) : text;
    }

    // Üres, ha ebben a nézetben még nem volt mérés.
    public static string BuildStatus(LastSessionSummary? last)
    {
        if (last is null)
        {
            return string.Empty;
        }

        var notes = string.Empty;
        if (last.MissingEspTrials > 0)
        {
            notes += $" Figyelem: {last.MissingEspTrials} mért próbánál nincs ESP-reakcióidő.";
        }

        if (last.NoGazeDataTrials > 0)
        {
            notes += $" Figyelem: {last.NoGazeDataTrials} próbában nem érkezett tekintetadat – ellenőrizd a szemkövető csatlakozását.";
        }

        return last.Status == SessionStatus.Completed
            ? $"A mérés elkészült: {last.ValidTrials}/{last.PlannedTrials} érvényes próba"
                + (last.EspMedianMs is { } median ? $", ESP medián: {median:0} ms" : string.Empty)
                + ". Az adatok elmentve." + notes
            : $"A mérés megszakadt; az eddigi próbák elmentve ({last.ValidTrials} érvényes)." + notes;
    }
}
