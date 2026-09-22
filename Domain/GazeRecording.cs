namespace ReactionTimeMeasurementApp.Domain;

// Egyetlen Tobii tekintetminta. Az időbélyeg a Tobii SDK rendszerórája szerint értendő
// (GazeDataEventArgs.SystemTimeStamp, µs) - ugyanezen az órán kéri le a mérés az inger
// kirajzolásának és a válasz beérkezésének idejét (EyeTrackingOperations.GetSystemTimeStamp),
// így a minták pontosan a próba eseményeihez igazíthatók. Az érvénytelen mintákat (pislogás,
// elveszett szem) is rögzítjük NaN koordinátákkal, mert ezekből számolható az adatvesztés aránya.
// Az EyeDistanceMm a szem(ek) merőleges távolsága a kijelző síkjától (a Tobii display area alapján);
// NaN, ha nem ismert.
public readonly record struct GazeSample(
    long SystemTimeStampUs,
    float NormalizedX,
    float NormalizedY,
    bool LeftEyeValid,
    bool RightEyeValid,
    float EyeDistanceMm = float.NaN)
{
    public bool IsValid => (LeftEyeValid || RightEyeValid)
        && float.IsFinite(NormalizedX)
        && float.IsFinite(NormalizedY);
}

// Egy próba teljes (előperiódus + inger + válasz) tekintetfelvétele. A "válaszablak" az inger
// kirajzolásától a válasz beérkezéséig (vagy az időtúllépésig) tart - a próba tekintet alapú
// érvényessége kizárólag az ebbe az ablakba eső mintákból dől el, nem egyetlen, a válasz után
// kiolvasott pontból.
public sealed class TrialGazeRecording
{
    public TrialGazeRecording(
        long stimulusOnsetUs,
        long responseWindowEndUs,
        Rectangle gazeScreenBounds,
        Point stageScreenOrigin,
        IReadOnlyList<GazeSample> samples)
    {
        StimulusOnsetUs = stimulusOnsetUs;
        ResponseWindowEndUs = responseWindowEndUs;
        GazeScreenBounds = gazeScreenBounds;
        StageScreenOrigin = stageScreenOrigin;
        Samples = samples;

        var validPoints = new List<PointF>();
        var windowSampleCount = 0;
        var eyeDistanceSum = 0d;
        var eyeDistanceCount = 0;
        foreach (var sample in samples)
        {
            if (!IsInResponseWindow(sample))
            {
                continue;
            }

            windowSampleCount++;
            if (sample.IsValid)
            {
                validPoints.Add(ToStagePoint(sample));
            }

            if (float.IsFinite(sample.EyeDistanceMm))
            {
                eyeDistanceSum += sample.EyeDistanceMm;
                eyeDistanceCount++;
            }
        }

        ResponseWindowSampleCount = windowSampleCount;
        ValidResponseWindowPoints = validPoints;
        ValidResponseWindowRatio = windowSampleCount == 0 ? 0 : validPoints.Count / (double)windowSampleCount;
        MeanEyeDistanceMm = eyeDistanceCount == 0 ? null : eyeDistanceSum / eyeDistanceCount;
    }

    public long StimulusOnsetUs { get; }

    public long ResponseWindowEndUs { get; }

    // A normalizált Tobii-koordináták képpontra váltásához használt kijelző területe.
    public Rectangle GazeScreenBounds { get; }

    // A mérőfelület (stage) bal felső sarkának képernyő-koordinátája a próba idején.
    public Point StageScreenOrigin { get; }

    public IReadOnlyList<GazeSample> Samples { get; }

    public int ResponseWindowSampleCount { get; }

    // A válaszablak érvényes mintái a mérőfelület koordinátáiban, időrendben.
    public IReadOnlyList<PointF> ValidResponseWindowPoints { get; }

    // A válaszablakba eső minták közül az érvényesek aránya (0..1). Ha az ablakba egyetlen minta
    // sem esett (pl. a szemkövető nem küldött adatot), az értéke 0.
    public double ValidResponseWindowRatio { get; }

    // A szem átlagos távolsága a kijelző síkjától a válaszablakban (mm); null, ha nem ismert.
    // Ebből utólag ellenőrizhető, mennyit mozdult el a résztvevő a mérés elején mért távolsághoz képest.
    public double? MeanEyeDistanceMm { get; }

    public bool IsInResponseWindow(GazeSample sample)
    {
        return sample.SystemTimeStampUs >= StimulusOnsetUs && sample.SystemTimeStampUs <= ResponseWindowEndUs;
    }

    public double GetTimeFromOnsetMs(GazeSample sample)
    {
        return (sample.SystemTimeStampUs - StimulusOnsetUs) / 1000d;
    }

    public PointF ToStagePoint(GazeSample sample)
    {
        return new PointF(
            GazeScreenBounds.Left + (sample.NormalizedX * GazeScreenBounds.Width) - StageScreenOrigin.X,
            GazeScreenBounds.Top + (sample.NormalizedY * GazeScreenBounds.Height) - StageScreenOrigin.Y);
    }
}
