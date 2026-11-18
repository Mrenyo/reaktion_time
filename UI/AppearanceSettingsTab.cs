using ReactionTimeMeasurementApp.Domain;
using ReactionTimeMeasurementApp.Measurement;

namespace ReactionTimeMeasurementApp.UI;

// A beállítási ablak "Megjelenés" füle: az inger és a háttér színe, az inger mérete és távolsága.
//
// A színmezők szövege gépelés közben még nem érvényes, ezért a fül a véglegesített színszöveget külön
// tárolja: egy teljes, érvényes szín már gépelés közben érvényesül, az egységes "#RRGGBB" alakra hozás a
// mező elhagyásakor vagy Enterre történik; a hiányos vagy hibás szöveg csak véglegesítéskor számít, és
// változatlanul kerül a beállításokba (a Validate() hibaként jelzi).
internal sealed partial class AppearanceSettingsTab : SettingsTab
{
    private static readonly Color InvalidSwatchColor = Color.FromArgb(235, 235, 235);
    private static readonly Color InvalidSwatchTextColor = Color.FromArgb(120, 120, 120);

    // Színmezőnként a véglegesített szöveg és a mező színmintája.
    private readonly Dictionary<TextBox, string> _committedColors;
    private readonly Dictionary<TextBox, Button> _swatches;

    public AppearanceSettingsTab()
    {
        InitializeComponent();
        BindNumbers(
            (SettingsNumber.DiameterPx, diameterNumeric),
            (SettingsNumber.MinEccentricityPx, minEccentricityNumeric),
            (SettingsNumber.MaxEccentricityPx, maxEccentricityNumeric));

        _committedColors = new() { [stimulusColorTextBox] = string.Empty, [backgroundColorTextBox] = string.Empty };
        _swatches = new() { [stimulusColorTextBox] = stimulusColorSwatchButton, [backgroundColorTextBox] = backgroundColorSwatchButton };
    }

    public override void ShowValues(SettingsEditorValues values)
    {
        RunWithoutEdits(() =>
        {
            base.ShowValues(values);
            ShowColorText(stimulusColorTextBox, NormalizeColorText(values.StimulusColor));
            ShowColorText(backgroundColorTextBox, NormalizeColorText(values.BackgroundColor));
        });
    }

    public override void ReadValues(SettingsEditorValues values, bool commit)
    {
        base.ReadValues(values, commit);
        values.StimulusColor = _committedColors[stimulusColorTextBox];
        values.BackgroundColor = _committedColors[backgroundColorTextBox];
    }

    // A színmezőben az Enter a beírt színt véglegesíti.
    public override bool CommitFocusedInput()
    {
        if (ActiveControl is TextBox textBox && _committedColors.ContainsKey(textBox) && textBox.Focused)
        {
            CommitColorText(textBox);
            textBox.SelectAll();
            return true;
        }

        return base.CommitFocusedInput();
    }

    private static string NormalizeColorText(string? text)
    {
        return HexColor.TryParse(text, out var color) ? HexColor.Format(color) : (text ?? string.Empty).Trim();
    }

    // A mező szövege és a véglegesített szöveg programból (nem számít módosításnak).
    private void ShowColorText(TextBox textBox, string text)
    {
        _committedColors[textBox] = text;
        RunWithoutEdits(() => textBox.Text = text);
        UpdateSwatch(textBox);
    }

    // A színmező szövegének véglegesítése: érvényes szín egységes "#RRGGBB" alakra hozva; érvénytelen
    // szöveg változatlanul (csak a szélső szóközök nélkül).
    private void CommitColorText(TextBox textBox)
    {
        if (IsDisposed || IsPopulating)
        {
            return;
        }

        var normalized = NormalizeColorText(textBox.Text);
        if (textBox.Text != normalized)
        {
            RunWithoutEdits(() => textBox.Text = normalized);
        }

        SetCommittedColor(textBox, normalized);
    }

    private void SetCommittedColor(TextBox textBox, string text)
    {
        if (string.Equals(_committedColors[textBox], text, StringComparison.Ordinal))
        {
            return;
        }

        _committedColors[textBox] = text;
        UpdateSwatch(textBox);
        RaiseEdited();
    }

    // A színminta gomb a véglegesített színt mutatja; értelmezhetetlen színnél szürke, kérdőjellel.
    private void UpdateSwatch(TextBox textBox)
    {
        var swatch = _swatches[textBox];
        var valid = HexColor.TryParse(_committedColors[textBox], out var color);
        var fill = valid ? color : InvalidSwatchColor;
        swatch.BackColor = fill;
        swatch.FlatAppearance.MouseOverBackColor = fill;
        swatch.FlatAppearance.MouseDownBackColor = fill;
        swatch.ForeColor = valid ? fill : InvalidSwatchTextColor;
        swatch.Text = valid ? string.Empty : "?";
    }

    private void PickColor(TextBox textBox)
    {
        CommitColorText(textBox);
        if (HexColor.TryParse(textBox.Text, out var current))
        {
            colorDialog.Color = current;
        }

        // Az érvényes szín a szöveg változásakor rögtön véglegesül (colorTextBox_TextChanged).
        if (colorDialog.ShowDialog(FindForm()) == DialogResult.OK)
        {
            textBox.Text = HexColor.Format(Color.FromArgb(255, colorDialog.Color));
        }
    }

    private void colorTextBox_TextChanged(object? sender, EventArgs e)
    {
        if (!IsPopulating && sender is TextBox textBox && HexColor.TryParse(textBox.Text, out var color))
        {
            SetCommittedColor(textBox, HexColor.Format(color));
        }
    }

    private void colorTextBox_Validated(object? sender, EventArgs e)
    {
        if (sender is TextBox textBox)
        {
            CommitColorText(textBox);
        }
    }

    private void stimulusColorSwatchButton_Click(object? sender, EventArgs e) => PickColor(stimulusColorTextBox);

    private void backgroundColorSwatchButton_Click(object? sender, EventArgs e) => PickColor(backgroundColorTextBox);

    private void swapColorsButton_Click(object? sender, EventArgs e)
    {
        CommitColorText(stimulusColorTextBox);
        CommitColorText(backgroundColorTextBox);
        var stimulusColor = _committedColors[stimulusColorTextBox];
        ShowColorText(stimulusColorTextBox, _committedColors[backgroundColorTextBox]);
        ShowColorText(backgroundColorTextBox, stimulusColor);
        RaiseEdited();
    }
}
