using System.Globalization;
using ReactionTimeMeasurementApp.Domain;

namespace ReactionTimeMeasurementApp.Measurement;

internal static class SessionMetadata
{
    // A mérés körülményei a mérési fájl fejlécébe; a kulcsok sorrendje rögzített, az értékek
    // invariáns formátumúak. A *-gal jelölt kulcsok csak akkor szerepelnek, ha van értékük:
    // DisplaySizeMm, DisplaySizeSource, DisplayAreaWarning, ScanoutTimingWarning.
    public static List<KeyValuePair<string, string>> Build(
        SessionContext context,
        IGazeTracker gazeTracker,
        IResponseDevice device,
        bool calibrationConfirmed,
        string appVersion)
    {
        var settings = context.Settings;
        var trials = settings.Trials;
        var stimulus = settings.Stimulus;
        var gaze = settings.Gaze;
        var geometry = context.Geometry;
        var timing = geometry.Timing;
        var stage = geometry.Stage;
        var photodiode = settings.Hardware.ResolvePhotodiodeBounds(stage.StageSize);
        var metadata = new List<KeyValuePair<string, string>>();

        void Add(string key, string value) => metadata.Add(new KeyValuePair<string, string>(key, value));
        static string Number(double value, string format = "0.###") => value.ToString(format, CultureInfo.InvariantCulture);
        static string Integer(int value) => value.ToString(CultureInfo.InvariantCulture);

        Add("AppVersion", appVersion);
        Add("Tracker", gazeTracker.TrackerDescription);
        Add("GazeOutputFrequencyHz", gazeTracker.GazeOutputFrequencyHz.HasValue ? Number(gazeTracker.GazeOutputFrequencyHz.Value) : string.Empty);
        Add("CalibrationConfirmed", calibrationConfirmed.ToString());
        Add("SerialPort", device.PortName);
        Add("SerialBaudRate", Integer(device.BaudRate));
        Add("EspFirmwareVersion", device.FirmwareVersion ?? string.Empty);
        Add("ScreenDevice", stage.ScreenDeviceName);
        Add("ScreenResolutionPx", $"{stage.ScreenBounds.Width}x{stage.ScreenBounds.Height}");
        if (geometry.DisplaySize is { } displaySize)
        {
            Add("DisplaySizeMm", $"{Number(displaySize.WidthMm, "0.0")}x{Number(displaySize.HeightMm, "0.0")}");
            Add("DisplaySizeSource", "Tobii display area");
        }

        if (geometry.DisplayAreaWarning is not null)
        {
            Add("DisplayAreaWarning", geometry.DisplayAreaWarning);
        }

        Add("RefreshRateHz", Number(timing.RefreshRateHz, "0.####"));
        Add("VerticalActiveLines", Integer(timing.VerticalActiveLines));
        Add("VerticalTotalLines", Integer(timing.VerticalTotalLines));
        Add("DesktopHeightPx", Integer(timing.DesktopHeightPx));
        Add("ScanoutTimingSource", timing.Source);
        Add("ScanoutTimingExact", timing.IsExact.ToString());
        if (timing.Warning is not null)
        {
            Add("ScanoutTimingWarning", timing.Warning);
        }

        Add("ScanoutReferenceRow", "StimulusCenter");
        Add("StageSizePx", $"{stage.StageSize.Width}x{stage.StageSize.Height}");
        Add("StageScreenOriginPx", $"{stage.StageScreenOrigin.X},{stage.StageScreenOrigin.Y}");
        Add("PhotodiodeMarkerStagePx", $"{photodiode.X},{photodiode.Y},{photodiode.Width},{photodiode.Height}");
        Add("PhotodiodeGuardPx", Integer(PhotodiodeMarker.GuardPx));
        Add("StageSafeMarginPx", Number(StageLayout.SafeMarginPx));
        Add("FixationCrossPx", $"{Number(FixationCross.ArmLengthPx)},{Number(FixationCross.ThicknessPx)},{Number(FixationCross.OutlinePx)}");
        Add("MeasuredTrials", Integer(trials.MeasuredTrials));
        Add("PracticeTrials", Integer(trials.PracticeTrials));
        Add("ResponseMode", trials.ResponseMode.ToString());
        Add("ReactionTimeoutMs", Integer(trials.ReactionTimeoutMs));
        Add("AnticipationThresholdMs", Number(ReactionTimeLimits.AnticipationThresholdMs));
        Add("SceneSettleDelayMs", Integer(trials.SceneSettleDelayMs));
        Add("ForeperiodDistribution", trials.ForeperiodDistribution.ToString());
        Add("ForeperiodMinMs", Integer(trials.ForeperiodMinMs));
        Add("ForeperiodMaxMs", Integer(trials.ForeperiodMaxMs));
        Add("ForeperiodExponentialMeanMs", Integer(trials.ForeperiodExponentialMeanMs));
        Add("RandomSeed", Integer(context.RandomSeed));
        Add("StimulusColor", HexColor.Format(context.StimulusColor));
        Add("BackgroundColor", HexColor.Format(context.BackgroundColor));
        Add("StimulusDiameterPx", Integer(stimulus.DiameterPx));
        Add("MinEccentricityPx", Integer(stimulus.MinEccentricityPx));
        Add("MaxEccentricityPx", Integer(stimulus.MaxEccentricityPx));
        Add("EffectiveMaxEccentricityPx", Number(geometry.EffectiveMaxEccentricityPx));
        Add("StimulusPlacementSampling", "UniformDistance");
        Add("GazeValidation", gaze.Validation.ToString());
        Add("GazeDataRecorded", context.GazeActive.ToString());
        Add("FixationPlacement", gaze.EffectivePlacement.ToString());
        Add("FixationPlacementSetting", gaze.FixationPlacement.ToString());
        Add("FixationPointStagePx", $"{Number(geometry.FixationPoint.X)},{Number(geometry.FixationPoint.Y)}");
        Add("GazeValidityRadiusPx", Integer(gaze.ValidityRadiusPx));
        Add("MinValidGazeSampleRatio", Number(gaze.MinValidGazeSampleRatio));
        return metadata;
    }
}
