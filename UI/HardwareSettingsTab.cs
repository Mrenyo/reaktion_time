using System.Globalization;
using ReactionTimeMeasurementApp.Measurement;

namespace ReactionTimeMeasurementApp.UI;

// A beállítási ablak "Hardver" füle: az ESP soros portja és átviteli sebessége, a fotodióda-jelző helye.
internal sealed partial class HardwareSettingsTab : SettingsTab
{
    private const string AutomaticPortText = "Automatikus felismerés";

    private static readonly int[] BaudRateChoices = [9600, 57600, 115200, 230400, 460800, 921600];

    public HardwareSettingsTab()
    {
        InitializeComponent();
        espPortComboBox.Items.Add(AutomaticPortText);
        foreach (var baudRate in BaudRateChoices)
        {
            baudRateComboBox.Items.Add(baudRate.ToString(CultureInfo.InvariantCulture));
        }

        // A szerkeszthető legördülő lista létrehozáskor (a kiválasztott elem beállításakor) és
        // átméretezéskor kijelöli a teljes szövegét, és ez fókusz nélkül is kiemelve látszana. A
        // létrehozáskor a kijelölés a HandleCreated esemény után történik, ezért a törlés későbbre kerül.
        foreach (var comboBox in new[] { espPortComboBox, baudRateComboBox })
        {
            comboBox.HandleCreated += (_, _) => comboBox.BeginInvoke(() => ClearComboBoxSelection(comboBox));
            comboBox.Resize += (_, _) => ClearComboBoxSelection(comboBox);
        }
    }

    public event EventHandler? MovePhotodiodeRequested;

    public event EventHandler? ResetPhotodiodeRequested;

    // A gépen elérhető soros portok (az "Automatikus felismerés" után).
    public void SetAvailablePortNames(IReadOnlyList<string> portNames)
    {
        foreach (var portName in portNames)
        {
            espPortComboBox.Items.Add(portName);
        }
    }

    public override void ShowValues(SettingsEditorValues values)
    {
        RunWithoutEdits(() =>
        {
            base.ShowValues(values);
            if (values.EspPortName is not { } portName)
            {
                espPortComboBox.SelectedIndex = 0;
            }
            else if (espPortComboBox.FindStringExact(portName) is var index and >= 0)
            {
                espPortComboBox.SelectedIndex = index;
            }
            else
            {
                // A port most nincs a gépen (pl. az ESP nincs csatlakoztatva): a mentett név marad.
                espPortComboBox.Text = portName;
            }

            baudRateComboBox.Text = values.BaudRateText;
        });
    }

    public override void ReadValues(SettingsEditorValues values, bool commit)
    {
        base.ReadValues(values, commit);
        var portText = espPortComboBox.Text.Trim();
        values.EspPortName = portText.Length == 0 || string.Equals(portText, AutomaticPortText, StringComparison.OrdinalIgnoreCase)
            ? null
            : portText;
        values.BaudRateText = baudRateComboBox.Text;
    }

    public override void ApplyState(SettingsEditorViewState state)
    {
        photodiodePositionLabel.Text = "Jelenlegi hely: " + MeasurementDisplayText.FormatPhotodiodePosition(
            state.PhotodiodeBounds, state.ScreenSize, state.PhotodiodeIsDefault);
        resetPhotodiodeButton.Enabled = !state.PhotodiodeIsDefault;
    }

    private static void ClearComboBoxSelection(ComboBox comboBox)
    {
        if (!comboBox.Focused && comboBox.IsHandleCreated && comboBox.SelectionLength > 0)
        {
            comboBox.SelectionLength = 0;
        }
    }

    // Az átviteli sebesség mezőjébe csak számjegy gépelhető (a beillesztett szöveget az ellenőrzés jelzi).
    private void baudRateComboBox_KeyPress(object? sender, KeyPressEventArgs e)
    {
        if (!char.IsControl(e.KeyChar) && !char.IsAsciiDigit(e.KeyChar))
        {
            e.Handled = true;
        }
    }

    private void movePhotodiodeButton_Click(object? sender, EventArgs e) => MovePhotodiodeRequested?.Invoke(this, EventArgs.Empty);

    private void resetPhotodiodeButton_Click(object? sender, EventArgs e) => ResetPhotodiodeRequested?.Invoke(this, EventArgs.Empty);
}
