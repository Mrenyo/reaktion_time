namespace ReactionTimeMeasurementApp;

public sealed class MeasurementResult
{
    public MeasurementResult(int trialNumber, int totalTrials, double? espReactionTimeMs, double? localReactionTimeMs, DateTime recordedAt, bool valid)
    {
        TrialNumber = trialNumber;
        TotalTrials = totalTrials;
        EspReactionTimeMs = espReactionTimeMs;
        LocalReactionTimeMs = localReactionTimeMs;
        RecordedAt = recordedAt;
        Valid = valid;
    }

    public MeasurementResult(int trialNumber, int totalTrials, double reactionTimeMs, DateTime recordedAt, bool valid)
        : this(trialNumber, totalTrials, reactionTimeMs, reactionTimeMs, recordedAt, valid)
    {
    }

    public int TrialNumber { get; }

    public int TotalTrials { get; }

    public double? EspReactionTimeMs { get; }

    public double? LocalReactionTimeMs { get; }

    public double ReactionTimeMs => EspReactionTimeMs ?? LocalReactionTimeMs ?? 0;

    public DateTime RecordedAt { get; }
    public bool Valid { get; }
}

public sealed class MeasurementSessionResult
{
    public MeasurementSessionResult(string modeName, IReadOnlyList<MeasurementResult> trials, DateTime completedAt)
    {
        ModeName = modeName;
        Trials = trials;
        CompletedAt = completedAt;
    }

    public string ModeName { get; }

    public IReadOnlyList<MeasurementResult> Trials { get; }

    public DateTime CompletedAt { get; }

    public double AverageEspReactionTimeMs => GetAverage(result => result.EspReactionTimeMs);

    public double AverageLocalReactionTimeMs => GetAverage(result => result.LocalReactionTimeMs);

    public double AverageReactionTimeMs => AverageEspReactionTimeMs > 0 ? AverageEspReactionTimeMs : AverageLocalReactionTimeMs;

    private double GetAverage(Func<MeasurementResult, double?> selector)
    {
        var values = Trials
            .Select(selector)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToArray();

        return values.Length == 0 ? 0 : values.Average();
    }
}

public sealed class MeasurementSessionResultEventArgs : EventArgs
{
    public MeasurementSessionResultEventArgs(MeasurementSessionResult session)
    {
        Session = session;
    }

    public MeasurementSessionResult Session { get; }
}
