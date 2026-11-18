using ReactionTimeMeasurementApp.Domain;
using ReactionTimeMeasurementApp.Measurement;

namespace ReactionTimeMeasurementApp.UI;

// A beállítási ablak "Szemmozgás" füle: a tekintet-ellenőrzés módja, a fixációs pont helye, az
// elfogadási rádiusz és az érvényes tekintetminták aránya.
internal sealed partial class GazeSettingsTab : SettingsTab
{
    public GazeSettingsTab()
    {
        InitializeComponent();
        BindNumbers(
            (SettingsNumber.ValidityRadiusPx, validityRadiusNumeric),
            (SettingsNumber.MinValidGazeSampleRatioPercent, minGazeRatioNumeric));
    }

    public event EventHandler? PickFixationPointRequested;

    // A kutató maga választotta az egyéni helyet; a rádiógomb eseménykezelése után jelez (hogy a
    // kattintás feldolgozása befejeződjön), és csak akkor, ha a választás addig nem változott.
    public event EventHandler? CustomPlacementSelected;

    public override void ShowValues(SettingsEditorValues values)
    {
        RunWithoutEdits(() =>
        {
            base.ShowValues(values);
            var validation = values.GazeValidation;
            gazeValidationCheckBox.Checked = validation != GazeValidationMode.None;
            keepFixationRadioButton.Checked = validation == GazeValidationMode.KeepFixation;
            lookAtStimulusRadioButton.Checked = validation != GazeValidationMode.KeepFixation;
            customPlacementRadioButton.Checked = values.FixationPlacement == FixationPlacement.Custom;
            centerPlacementRadioButton.Checked = values.FixationPlacement != FixationPlacement.Custom;
        });
    }

    public override void ReadValues(SettingsEditorValues values, bool commit)
    {
        base.ReadValues(values, commit);
        values.GazeValidation = !gazeValidationCheckBox.Checked
            ? GazeValidationMode.None
            : keepFixationRadioButton.Checked ? GazeValidationMode.KeepFixation : GazeValidationMode.LookAtStimulus;
        values.FixationPlacement = customPlacementRadioButton.Checked ? FixationPlacement.Custom : FixationPlacement.Center;
    }

    // A nem alkalmazható vezérlők tiltása: tekintet-ellenőrzés nélkül a szemmozgás-beállítások, az
    // ingernézéses módban a fixációs pont helye (az egyéni hely csak fixációs módban érvényesül).
    public override void ApplyState(SettingsEditorViewState state)
    {
        foreach (var control in new Control[]
        {
            gazeTargetHeaderLabel, gazeTargetPanel,
            validityRadiusLabel, validityRadiusNumeric, validityRadiusUnitLabel, validityRadiusHintLabel,
            minGazeRatioLabel, minGazeRatioNumeric, minGazeRatioUnitLabel, minGazeRatioHintLabel
        })
        {
            control.Enabled = state.GazeOptionsEnabled;
        }

        fixationPlacementHeaderLabel.Enabled = state.FixationPlacementEnabled;
        fixationPlacementPanel.Enabled = state.FixationPlacementEnabled;
        pickFixationPointButton.Enabled = state.PickFixationPointEnabled;
        validityRadiusHintLabel.Text = state.ValidityRadiusHint;
        fixationPointLabel.Text = state.FixationPointPixel is { } pixel
            ? MeasurementDisplayText.FormatStagePoint(pixel, state.ScreenSize)
            : "még nincs kijelölve";
    }

    // A fixációs pont helye vissza középre (nem számít módosításnak).
    public void SelectCenterPlacement()
    {
        RunWithoutEdits(() => centerPlacementRadioButton.Checked = true);
    }

    private void gazeOption_CheckedChanged(object? sender, EventArgs e)
    {
        // A rádiógomb-csoportban a kikapcsolt gomb is jelez; elég a bekapcsoltra reagálni.
        if (sender is not RadioButton { Checked: false })
        {
            RaiseEdited();
        }
    }

    private void customPlacementRadioButton_CheckedChanged(object? sender, EventArgs e)
    {
        if (IsPopulating || !customPlacementRadioButton.Checked)
        {
            return;
        }

        RaiseEdited();
        BeginInvoke(() =>
        {
            if (!IsDisposed && customPlacementRadioButton.Checked)
            {
                CustomPlacementSelected?.Invoke(this, EventArgs.Empty);
            }
        });
    }

    private void pickFixationPointButton_Click(object? sender, EventArgs e) => PickFixationPointRequested?.Invoke(this, EventArgs.Empty);
}
