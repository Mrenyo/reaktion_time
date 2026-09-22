using ReactionTimeMeasurementApp.Classes;

namespace ReactionTimeMeasurementApp;

// "Normál reakcióidő" mérés: a résztvevő egyenesen ránéz az ingerre, és úgy reagál rá - itt tehát
// az számít érvényesnek, ha a tekintet ténylegesen az inger helyén van (nem a stage közepén).
public partial class NormalMeasureView : MeasurementViewBase
{
    internal NormalMeasureView(TobiiService tobiiService, bool manageTrackingLifecycle)
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
        SessionName: "Normál mérés",
        IdleInstructionText: "Nézz a középső keresztre. Amint megjelenik a fehér pont, nézz rá, és a lehető leggyorsabban kattints. "
            + "Utána nézz vissza a keresztre.",
        FixationCrossSizePx: 18f,
        FixationCrossThicknessPx: 3f,
        StimulusSafeMarginPx: 36f,
        Mode: settings.NormalMode);

    // Normál mérésnél a tekintetnek az inger megjelenése és a válasz között legalább egyszer az
    // inger tényleges helyére (középpontjától GazeValidityRadiusDeg fokon belülre) kell esnie
    // ahhoz, hogy a próba érvényesnek számítson.
    protected override bool IsGazeValidForTrial(IReadOnlyList<PointF> validGazePointsOnStage, RectangleF stimulusRect, StageVisualSpace visualSpace)
    {
        var stimulusCenter = new PointF(
            stimulusRect.X + (stimulusRect.Width / 2f),
            stimulusRect.Y + (stimulusRect.Height / 2f));
        var radiusDeg = Profile.Mode.GazeValidityRadiusDeg;
        return validGazePointsOnStage.Any(point => visualSpace.AngleBetweenDeg(point, stimulusCenter) < radiusDeg);
    }

    private void NormalMeasureView_Paint(object? sender, PaintEventArgs e) => DrawScene(e.Graphics);

    private async void startMeasureButton_Click(object? sender, EventArgs e) => await HandleStartMeasureButtonClickAsync();

    private void backButton_Click(object? sender, EventArgs e) => HandleBackButtonClick();
}
