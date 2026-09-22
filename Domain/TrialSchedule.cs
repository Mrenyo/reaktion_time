namespace ReactionTimeMeasurementApp.Domain;

public enum TrialPhase
{
    Practice,
    Test
}

// A próbák sorrendje: először a gyakorló próbák, utána a tesztpróbák. Ha az érvénytelen
// tesztpróbák ismétlése be van kapcsolva, addig jön új próba, amíg össze nem gyűlik a kívánt
// számú érvényes (legfeljebb MaxTestAttempts próbálkozásig).
internal sealed class TrialSchedule
{
    private readonly ModeSettings _settings;

    public TrialSchedule(ModeSettings settings)
    {
        _settings = settings;
    }

    public int PracticeTrialsDone { get; private set; }

    public int TestAttempts { get; private set; }

    public int ValidTestTrials { get; private set; }

    public TrialPhase CurrentPhase => PracticeTrialsDone < _settings.PracticeTrials ? TrialPhase.Practice : TrialPhase.Test;

    public int NextTrialNumber => CurrentPhase == TrialPhase.Practice ? PracticeTrialsDone + 1 : TestAttempts + 1;

    public bool IsCompleted => PracticeTrialsDone >= _settings.PracticeTrials && IsTestTargetReached;

    public bool HasNextTrial
    {
        get
        {
            if (CurrentPhase == TrialPhase.Practice)
            {
                return true;
            }

            if (IsTestTargetReached)
            {
                return false;
            }

            var attemptLimit = _settings.RepeatInvalidTrials ? _settings.MaxTestAttempts : _settings.TestTrials;
            return TestAttempts < attemptLimit;
        }
    }

    private bool IsTestTargetReached => _settings.RepeatInvalidTrials
        ? ValidTestTrials >= _settings.TestTrials
        : TestAttempts >= _settings.TestTrials;

    public void Record(TrialPhase phase, bool valid)
    {
        if (phase == TrialPhase.Practice)
        {
            PracticeTrialsDone++;
            return;
        }

        TestAttempts++;
        if (valid)
        {
            ValidTestTrials++;
        }
    }

    public string ProgressText => CurrentPhase == TrialPhase.Practice
        ? $"Gyakorlás {PracticeTrialsDone}/{_settings.PracticeTrials}"
        : $"Érvényes {ValidTestTrials}/{_settings.TestTrials}";
}

internal static class ForeperiodSampler
{
    // Az inger előtti várakozási idő (előperiódus) mintavétele a beállított eloszlásból.
    internal static int Sample(Random random, ModeSettings settings)
    {
        var min = settings.ForeperiodMinMs;
        var max = settings.ForeperiodMaxMs;
        if (max <= min)
        {
            return min;
        }

        var u = random.NextDouble();
        if (settings.ForeperiodDistribution == ForeperiodDistribution.Uniform)
        {
            return (int)Math.Round(min + (u * (max - min)));
        }

        // Csonkolt exponenciális eloszlás inverz eloszlásfüggvénnyel (nincs újramintavételezés):
        // F(x) = (1 - e^(-(x-min)/mu)) / (1 - e^(-(max-min)/mu)).
        var mu = (double)settings.ForeperiodExponentialMeanMs;
        var truncation = 1 - Math.Exp(-(max - min) / mu);
        var value = min - (mu * Math.Log(1 - (u * truncation)));
        return (int)Math.Round(Math.Clamp(value, min, max));
    }
}
