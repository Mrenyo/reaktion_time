using ReactionTimeMeasurementApp.Classes;

namespace ReactionTimeMeasurementApp;

// "Periférikus reakcióidő" mérés: a résztvevőnek a középső fixációs kereszten kell tartania a
// tekintetét, és úgy kell reagálnia a szélen felvillanó ingerre, hogy közben nem néz oda - ezért
// itt a fixációs pont körüli tekintet számít "érvényesnek", nem az inger tényleges helye.
public partial class Measure_1 : MeasurementViewBase
{
    internal Measure_1(TobiiService tobiiService, bool manageTrackingLifecycle)
        : base(tobiiService, manageTrackingLifecycle)
    {
        InitializeComponent();
        InitializeMeasurementView();
    }

    protected override Panel StagePanel => stagePanel;

    protected override Label InstructionLabel => instructionLabel;

    protected override Button StartMeasureButton => startMeasureButton;

    protected override Button BackButton => backButton;

    protected override MeasurementProfile CreateProfile(ExperimentSettings settings) => new(
        SessionName: "Periférikus mérés",
        IdleInstructionText: "Nézz végig a középső keresztre. Amint bárhol megjelenik a fehér pont, a lehető leggyorsabban kattints – "
            + "de ne nézz oda a pontra, a tekinteted maradjon a kereszten.",
        FixationCrossSizePx: 18f,
        FixationCrossThicknessPx: 3f,
        StimulusSafeMarginPx: 36f,
        Mode: settings.PeripheralMode);

    // Ha az inger a fixációs ablakon belül (vagy annak szélén) is megjelenhetne, az ingerre nézés
    // sem hagyná el az ablakot, így a tekintet-ellenőrzés értelmét vesztené.
    protected override string? ValidateProfile(MeasurementProfile profile)
    {
        var mode = profile.Mode;
        var minimumEccentricity = mode.GazeValidityRadiusDeg + (mode.StimulusDiameterDeg / 2);
        return mode.MinEccentricityDeg > minimumEccentricity
            ? null
            : $"PeripheralMode: a MinEccentricityDeg ({mode.MinEccentricityDeg:0.#}°) legyen nagyobb, mint "
                + $"GazeValidityRadiusDeg + StimulusDiameterDeg / 2 ({minimumEccentricity:0.#}°), különben az ingerre nézés "
                + "is a fixációs ablakon belül maradhat.";
    }

    // Periférikus mérésnél a tekintetnek az inger megjelenésétől a válaszig VÉGIG a fixációs
    // kereszten kell maradnia - ha akár egyetlen érvényes minta is GazeValidityRadiusDeg foknál
    // messzebb esik, a résztvevő valószínűleg az ingerre nézett (akkor is, ha a válasz pillanatára
    // már visszanézett), ami érvényteleníti a próbát.
    protected override bool IsGazeValidForTrial(IReadOnlyList<PointF> validGazePointsOnStage, RectangleF stimulusRect, StageVisualSpace visualSpace)
    {
        var radiusDeg = Profile.Mode.GazeValidityRadiusDeg;
        return validGazePointsOnStage.All(point => visualSpace.AngleBetweenDeg(point, visualSpace.FixationPoint) < radiusDeg);
    }

    protected override void OnIdleStateShown()
    {
        statusValueLabel.Text = ProgressText;
    }

    protected override void OnTrialRecorded(MeasurementResult result)
    {
        statusValueLabel.Text = ProgressText;
        lastResultValueLabel.Text = $"ESP {FormatReaction(result.EspReactionTimeMs)} | PC {FormatReaction(result.LocalReactionTimeMs)}";
    }

    private void stagePanel_Paint(object? sender, PaintEventArgs e) => DrawScene(e.Graphics);

    private async void startMeasureButton_Click(object? sender, EventArgs e) => await HandleStartMeasureButtonClickAsync();

    private void backButton_Click(object? sender, EventArgs e) => HandleBackButtonClick();
}
