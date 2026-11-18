using ReactionTimeMeasurementApp.Domain;
using ReactionTimeMeasurementApp.Measurement;

namespace ReactionTimeMeasurementApp.UI;

// A beállítási ablak "Ingerek és időzítés" füle: válaszmód, próbaszámok, időkorlát, előperiódus.
internal sealed partial class TrialsSettingsTab : SettingsTab
{
    private const int ClickAnywhereIndex = 0;
    private const int ClickOnStimulusIndex = 1;

    private const int ExponentialDistributionIndex = 0;
    private const int UniformDistributionIndex = 1;

    public TrialsSettingsTab()
    {
        InitializeComponent();
        BindNumbers(
            (SettingsNumber.MeasuredTrials, measuredTrialsNumeric),
            (SettingsNumber.PracticeTrials, practiceTrialsNumeric),
            (SettingsNumber.ReactionTimeoutMs, reactionTimeoutNumeric),
            (SettingsNumber.ForeperiodMinMs, foreperiodMinNumeric),
            (SettingsNumber.ForeperiodMaxMs, foreperiodMaxNumeric),
            (SettingsNumber.ForeperiodExponentialMeanMs, foreperiodMeanNumeric),
            (SettingsNumber.SceneSettleDelayMs, sceneSettleDelayNumeric));

        responseModeComboBox.Items.AddRange(["Kattintás az inger megjelenésekor", "Kattintás az ingerre"]);
        foreperiodDistributionComboBox.Items.AddRange(["Exponenciális (kiszámíthatatlan)", "Egyenletes"]);
    }

    public override void ShowValues(SettingsEditorValues values)
    {
        RunWithoutEdits(() =>
        {
            base.ShowValues(values);
            responseModeComboBox.SelectedIndex = values.ResponseMode == ResponseMode.ClickOnStimulus
                ? ClickOnStimulusIndex
                : ClickAnywhereIndex;
            foreperiodDistributionComboBox.SelectedIndex = values.ForeperiodDistribution == ForeperiodDistribution.Uniform
                ? UniformDistributionIndex
                : ExponentialDistributionIndex;
        });
    }

    public override void ReadValues(SettingsEditorValues values, bool commit)
    {
        base.ReadValues(values, commit);
        values.ResponseMode = responseModeComboBox.SelectedIndex == ClickOnStimulusIndex
            ? ResponseMode.ClickOnStimulus
            : ResponseMode.ClickAnywhere;
        values.ForeperiodDistribution = foreperiodDistributionComboBox.SelectedIndex == UniformDistributionIndex
            ? ForeperiodDistribution.Uniform
            : ForeperiodDistribution.Exponential;
    }

    // Egyenletes eloszlásnál az exponenciális várható érték nem alkalmazható.
    public override void ApplyState(SettingsEditorViewState state)
    {
        responseModeHintLabel.Text = state.ResponseModeHint;
        trialCountNoteLabel.Text = state.TrialCountNote;
        foreperiodMeanLabel.Enabled = state.ForeperiodMeanEnabled;
        foreperiodMeanNumeric.Enabled = state.ForeperiodMeanEnabled;
        foreperiodMeanUnitLabel.Enabled = state.ForeperiodMeanEnabled;
    }
}
