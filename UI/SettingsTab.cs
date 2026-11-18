using System.Globalization;
using ReactionTimeMeasurementApp.Measurement;

namespace ReactionTimeMeasurementApp.UI;

// A beállítási ablak egy fülének közös alapja. A fül a vezérlői értékét a SettingsEditorValues-ba
// olvassa ki (ReadValues), és abból tölti ki őket (ShowValues); a beállítások összeállítása, az
// ellenőrzés és a függő vezérlők állapota a SettingsEditorPresenter dolga (ApplyState).
//
// Itt van a számmezők közös kezelése (BindNumbers): a tartomány a SettingsNumberField-ből, a
// véglegesített és a gépelés alatt álló érték nyilvántartása, a véglegesítés. A fül minden
// felhasználói módosításnál jelez (Edited); a vezérlők programból történő kitöltése alatt
// (RunWithoutEdits) a változásjelző események nem számítanak módosításnak.
//
// A fül a betűtípusát a párbeszédablaktól örökli (Segoe UI 10), a méretezést (DPI) is az ablak végzi
// (AutoScaleMode.Inherit), így a fülek tartalma pontosan úgy méreteződik, mint az ablak többi része.
internal class SettingsTab : UserControl
{
    // A letiltott lapos gombra rajzolt áttetsző fátyol (a lap hátterének színével).
    private static readonly Color DisabledButtonVeilColor = Color.FromArgb(160, UiTheme.CardBackground);

    private readonly List<(SettingsNumber Id, NumericUpDown Control)> _numbers = [];
    private readonly Dictionary<NumericUpDown, decimal> _committedValues = new();

    // Egy érték módosult (egy felhasználói művelet több jelzést is adhat; az ablak összevonja őket).
    public event EventHandler? Edited;

    protected bool IsPopulating { get; private set; }

    public virtual void ShowValues(SettingsEditorValues values)
    {
        RunWithoutEdits(() =>
        {
            foreach (var (id, control) in _numbers)
            {
                control.Value = values.Numbers[id];
                _committedValues[control] = control.Value;
            }
        });
    }

    // commit: false esetén a még gépelés alatt álló számot is figyelembe veszi.
    public virtual void ReadValues(SettingsEditorValues values, bool commit)
    {
        foreach (var (id, control) in _numbers)
        {
            values.Numbers[id] = commit ? _committedValues[control] : GetPendingValue(control);
        }
    }

    public virtual void ApplyState(SettingsEditorViewState state)
    {
    }

    // A gépelés alatt álló számok véglegesítése: a Value olvasása véglegesíti a szöveget (a ValueChanged
    // frissíti a véglegesített értéket).
    public void CommitNumbers()
    {
        foreach (var (_, control) in _numbers)
        {
            _ = control.Value;
        }
    }

    // Enter egy beviteli mezőben: a számmező a beírt értéket véglegesíti, a legördülő lista (ha nincs
    // lenyitva) nem csinál semmit - egyik sem menti és zárja be az ablakot. Igazat ad, ha kezelte.
    public virtual bool CommitFocusedInput()
    {
        switch (ActiveControl)
        {
            case NumericUpDown numeric when numeric.ContainsFocus:
                _ = numeric.Value;
                numeric.Select(0, numeric.Text.Length);
                return true;
            case ComboBox { DroppedDown: false } comboBox when comboBox.ContainsFocus:
                return true;
            default:
                return false;
        }
    }

    // A számmezők közös beállítása: a tartomány és a tizedesjegyek a SettingsNumberField-ből, egész lépésköz.
    protected void BindNumbers(params (SettingsNumber Id, NumericUpDown Control)[] fields)
    {
        foreach (var (id, control) in fields)
        {
            var field = SettingsNumberField.For(id);
            control.DecimalPlaces = field.DecimalPlaces;
            control.Increment = 1;
            control.Minimum = field.Min;
            control.Maximum = field.Max;

            // A véglegesített érték megváltozott (léptetés, vagy a begépelt szöveg véglegesítése).
            control.ValueChanged += (_, _) =>
            {
                _committedValues[control] = control.Value;
                RaiseEdited();
            };
            control.TextChanged += control_Edited;
            _numbers.Add((id, control));
        }
    }

    protected void RaiseEdited()
    {
        if (!IsPopulating)
        {
            Edited?.Invoke(this, EventArgs.Empty);
        }
    }

    // A vezérlők programból történő módosításának idejére felfüggeszti a változásjelzést; a korábbi
    // állapotot állítja vissza, így a hívások egymásba ágyazhatók.
    protected void RunWithoutEdits(Action action)
    {
        var previous = IsPopulating;
        IsPopulating = true;
        try
        {
            action();
        }
        finally
        {
            IsPopulating = previous;
        }
    }

    // Egy vezérlő tartalma (szöveg, kiválasztás) megváltozott.
    protected void control_Edited(object? sender, EventArgs e) => RaiseEdited();

    // A lapos (FlatStyle.Flat) gomb letiltott felirata a háttérszínből számolt sötét szín, így a gomb
    // letiltva is aktívnak látszana; a fátyol jelzi, hogy most nem használható.
    protected void disabledButton_Paint(object? sender, PaintEventArgs e)
    {
        if (sender is Button { Enabled: false } button)
        {
            using var veil = new SolidBrush(DisabledButtonVeilColor);
            e.Graphics.FillRectangle(veil, button.ClientRectangle);
        }
    }

    // A NumericUpDown gépelés közbeni szövegéből az az érték, amelyet a vezérlő a véglegesítéskor
    // felvenne: értelmezhetetlen szövegnél a korábbi érték marad, különben a tartományba szorított szám.
    // A Value tulajdonság itt nem olvasható, mert az olvasása véglegesítené (és átírná) a szöveget.
    private decimal GetPendingValue(NumericUpDown control)
    {
        var text = control.Text.Trim();
        if (text.Length == 0 || !decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out var parsed))
        {
            return _committedValues[control];
        }

        return Math.Clamp(parsed, control.Minimum, control.Maximum);
    }
}
