namespace ReactionTimeMeasurementApp.Domain;

public enum TrialPhase
{
    Practice,
    Test
}

// A próbák sorrendje: először a gyakorló próbák, utána pontosan measuredTrials mért (teszt) próba. Az
// érvénytelen próbák nem pótlódnak: a mérés így mindig ugyanannyi ingerből áll, az érvénytelenek csak
// az okukkal együtt rögzülnek.
internal sealed class TrialSchedule
{
    private readonly int _practiceTrials;
    private readonly int _measuredTrials;

    public TrialSchedule(int practiceTrials, int measuredTrials)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(practiceTrials);
        ArgumentOutOfRangeException.ThrowIfNegative(measuredTrials);
        _practiceTrials = practiceTrials;
        _measuredTrials = measuredTrials;
    }

    public int PracticeTrialsDone { get; private set; }

    public int TestTrialsDone { get; private set; }

    public int ValidTestTrials { get; private set; }

    public TrialPhase CurrentPhase => PracticeTrialsDone < _practiceTrials ? TrialPhase.Practice : TrialPhase.Test;

    // A következő próba sorszáma a fázisán belül (1-től).
    public int NextTrialNumber => CurrentPhase == TrialPhase.Practice ? PracticeTrialsDone + 1 : TestTrialsDone + 1;

    public bool HasNextTrial => PracticeTrialsDone < _practiceTrials || TestTrialsDone < _measuredTrials;

    // Minden tervezett próba lefutott (érvényességtől függetlenül).
    public bool IsCompleted => !HasNextTrial;

    public void Record(TrialPhase phase, bool valid)
    {
        if (phase == TrialPhase.Practice)
        {
            PracticeTrialsDone++;
            return;
        }

        TestTrialsDone++;
        if (valid)
        {
            ValidTestTrials++;
        }
    }

    // A naplóba: "Gyakorlás 2/3", illetve "Mérés 5/10" (a már lefutott próbák száma).
    public string ProgressText => CurrentPhase == TrialPhase.Practice
        ? $"Gyakorlás {PracticeTrialsDone}/{_practiceTrials}"
        : $"Mérés {TestTrialsDone}/{_measuredTrials}";
}

internal static class ForeperiodSampler
{
    // Az inger előtti várakozási idő (előperiódus) mintavétele a beállított eloszlásból.
    internal static int Sample(Random random, TrialSettings settings)
    {
        var min = settings.ForeperiodMinMs;
        var max = settings.ForeperiodMaxMs;
        if (max <= min)
        {
            return min;
        }

        var u = random.NextDouble();
        switch (settings.ForeperiodDistribution)
        {
            case ForeperiodDistribution.Uniform:
                return (int)Math.Round(min + (u * (max - min)));

            case ForeperiodDistribution.Exponential:
                // Csonkolt exponenciális eloszlás inverz eloszlásfüggvénnyel (nincs újramintavételezés):
                // F(x) = (1 - e^(-(x-min)/mu)) / (1 - e^(-(max-min)/mu)).
                var mu = (double)settings.ForeperiodExponentialMeanMs;
                var truncation = 1 - Math.Exp(-(max - min) / mu);
                var value = min - (mu * Math.Log(1 - (u * truncation)));
                return (int)Math.Round(Math.Clamp(value, min, max));

            default:
                // Az ExperimentSettings.Validate() után nem fordulhat elő.
                throw new ArgumentOutOfRangeException(
                    nameof(settings),
                    settings.ForeperiodDistribution,
                    "Ismeretlen előperiódus-eloszlás.");
        }
    }
}
