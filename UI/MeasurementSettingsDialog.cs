using ReactionTimeMeasurementApp.Domain;
using ReactionTimeMeasurementApp.Measurement;

namespace ReactionTimeMeasurementApp.UI;

// A mérési beállítások párbeszédablaka (főmenü, Testreszabás) - a SettingsEditorPresenter passzív
// nézete. Az ablak csak keret: a fülek (TrialsSettingsTab, AppearanceSettingsTab, GazeSettingsTab,
// HardwareSettingsTab) a saját vezérlőiket kezelik, az ablak ezekből olvassa ki és ezekbe tölti az
// értékeket, mutatja az előnézetet, az üzeneteket és a fülek hibajelét, és megnyitja a kijelölőket. A
// beállítások összeállítása, az ellenőrzés és a mentés a presenter dolga.
//
// A Mentés gombnak nincs DialogResult-ja: a presenter előbb véglegesíti a még gépelés alatt álló
// értékeket, újra ellenőriz, és csak sikeres mentés után zárja be az ablakot (CloseAsSaved).
//
// Billentyűzet: egy beviteli mezőben (szám, szín, legördülő lista) az Enter csak az értéket véglegesíti;
// máshol az Enter, bárhol a Ctrl+Enter ment.
internal sealed partial class MeasurementSettingsDialog : Form, ISettingsEditorView
{
    private const string WarningMarker = " ⚠";

    // A szalag legfeljebb ennyi sornyi magas; a hosszabb szöveg görgethető.
    private const int MaxBannerLines = 4;

    private static readonly Color ErrorColor = Color.FromArgb(190, 40, 40);
    private static readonly Color WarningColor = Color.FromArgb(168, 110, 20);
    private static readonly Color OkColor = Color.FromArgb(70, 120, 60);

    // A mérést futtató képernyő (a főablak képernyője): ehhez igazodik az ellenőrzés és az előnézet, és
    // ezen nyílnak meg a kijelölők. A fizikai méret a fotodióda-jelző áthelyezőjének mm-jelöléseihez kell
    // (null, ha nem ismert).
    private readonly Rectangle _screenBounds;
    private readonly PhysicalDisplaySize? _displaySize;

    private readonly IReadOnlyList<SettingsTab> _tabs;

    // A beállítások szakaszaihoz tartozó fülek; a címük a SettingsSections.GetTitle-ből jön, így a
    // hibaüzenetekben megnevezett fül biztosan egyezik a felirattal.
    private readonly Dictionary<SettingsSection, TabPage> _tabPages;

    // Igaz, ha egy módosítás utáni jelzés már be van ütemezve (lásd ScheduleSettingsEdited).
    private bool _editScheduled;

    // A legutóbb megjelenített üzenetek: a lista csak akkor épül újra, ha változott.
    private IReadOnlyList<(string Text, Color Color)> _shownMessages = [];

    // measurementScreenBounds: a mérést futtató képernyő teljes területe (a mérés ennek teljes felületén
    // fut). availablePortNames: a gépen elérhető soros portok. displaySize: a képernyő fizikai mérete.
    public MeasurementSettingsDialog(
        Rectangle measurementScreenBounds,
        IReadOnlyList<string> availablePortNames,
        PhysicalDisplaySize? displaySize)
    {
        InitializeComponent();

        _screenBounds = measurementScreenBounds;
        _displaySize = displaySize;
        _tabs = [trialsTab, appearanceTab, gazeTab, hardwareTab];
        _tabPages = new Dictionary<SettingsSection, TabPage>
        {
            [SettingsSection.Trials] = trialsTabPage,
            [SettingsSection.Appearance] = appearanceTabPage,
            [SettingsSection.Gaze] = gazeTabPage,
            [SettingsSection.Hardware] = hardwareTabPage
        };
        foreach (var (section, page) in _tabPages)
        {
            page.Text = SettingsSections.GetTitle(section);
        }

        hardwareTab.SetAvailablePortNames(availablePortNames);
        foreach (var tab in _tabs)
        {
            tab.Edited += (_, _) => ScheduleSettingsEdited();
        }

        gazeTab.PickFixationPointRequested += (_, _) => PickFixationPointRequested?.Invoke(this, EventArgs.Empty);
        gazeTab.CustomPlacementSelected += (_, _) => CustomFixationPlacementSelected?.Invoke(this, EventArgs.Empty);
        hardwareTab.MovePhotodiodeRequested += (_, _) => MovePhotodiodeRequested?.Invoke(this, EventArgs.Empty);
        hardwareTab.ResetPhotodiodeRequested += (_, _) => ResetPhotodiodeRequested?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? SettingsEdited;

    public event EventHandler? PickFixationPointRequested;

    public event EventHandler? CustomFixationPlacementSelected;

    public event EventHandler? MovePhotodiodeRequested;

    public event EventHandler? ResetPhotodiodeRequested;

    public event EventHandler? DefaultsRequested;

    public event EventHandler? SaveRequested;

    public Size MeasurementScreenSize => _screenBounds.Size;

    // A betöltés hibája vagy tájékoztatása a fülek fölött. A többsoros TextBox csak CRLF sortörést
    // jelenít meg, ezért a sorvégek egységesítve kerülnek bele.
    public void ShowBanner(string? text)
    {
        bannerTextBox.Text = text?.ReplaceLineEndings("\r\n") ?? string.Empty;
        bannerPanel.Visible = text is not null;
    }

    public void ShowValues(SettingsEditorValues values, bool includeHardware)
    {
        foreach (var tab in _tabs)
        {
            if (includeHardware || tab != hardwareTab)
            {
                tab.ShowValues(values);
            }
        }
    }

    public SettingsEditorValues ReadValues(bool commit)
    {
        var values = new SettingsEditorValues();
        foreach (var tab in _tabs)
        {
            tab.ReadValues(values, commit);
        }

        return values;
    }

    // A ValidateChildren a színmezőket, a fülek a számmezőket véglegesítik.
    public void CommitPendingEdits()
    {
        ValidateChildren();
        foreach (var tab in _tabs)
        {
            tab.CommitNumbers();
        }
    }

    public void ApplyState(SettingsEditorViewState state)
    {
        _editScheduled = false;
        foreach (var (section, page) in _tabPages)
        {
            var title = SettingsSections.GetTitle(section);
            var text = state.Errors.Any(error => error.Section == section) ? title + WarningMarker : title;
            if (page.Text != text)
            {
                page.Text = text;
            }
        }

        // A lapos (FlatStyle.Flat) gomb letiltva is a saját színeivel rajzolódik, ezért a tiltott
        // állapotot a halványabb háttér jelzi.
        saveButton.Enabled = state.CanSave;
        saveButton.BackColor = saveButton.Enabled ? UiTheme.Accent : UiTheme.AccentDisabled;
        saveButton.FlatAppearance.BorderColor = saveButton.BackColor;
        ShowMessages(state);
        previewPanel.UpdatePreview(state.Settings, state.ScreenSize, state.Feasibility);
        foreach (var tab in _tabs)
        {
            tab.ApplyState(state);
        }
    }

    public void SelectSection(SettingsSection section)
    {
        if (_tabPages.TryGetValue(section, out var page))
        {
            settingsTabControl.SelectedTab = page;
        }
    }

    public NormalizedPoint? PickFixationPoint(ExperimentSettings settings, NormalizedPoint? currentPoint)
    {
        using var picker = new FixationPointPickerForm(_screenBounds, settings, currentPoint);
        return picker.ShowDialog(this) == DialogResult.OK ? picker.SelectedPoint : null;
    }

    public bool TryPickPhotodiodeMarker(ExperimentSettings settings, out NormalizedPoint? center)
    {
        using var picker = new PhotodiodeMarkerPickerForm(_screenBounds, settings, _displaySize);
        var picked = picker.ShowDialog(this) == DialogResult.OK;
        center = picked ? picker.SelectedCenter : null;
        return picked;
    }

    public void SelectCenterFixationPlacement() => gazeTab.SelectCenterPlacement();

    public void ShowSaveFailed(string path, Exception exception) => ErrorPopup.ShowSettingsSaveFailed(this, path, exception);

    public void ShowUnexpectedError(Exception exception) => ErrorPopup.ShowGeneral(this, exception);

    public void CloseAsSaved() => DialogResult = DialogResult.OK;

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        LayoutBanner();

        // A párbeszédablak sosem lóg ki a főablak képernyőjének munkaterületéről (tálca nélküli rész);
        // kis képernyőn a fülek tartalma görgethető.
        var workingArea = Screen.FromControl(Owner ?? (Control)this).WorkingArea;
        var size = new Size(Math.Min(Width, workingArea.Width), Math.Min(Height, workingArea.Height));
        if (size != Size)
        {
            Size = size;
        }

        Location = new Point(
            Math.Clamp(Left, workingArea.Left, Math.Max(workingArea.Left, workingArea.Right - Width)),
            Math.Clamp(Top, workingArea.Top, Math.Max(workingArea.Top, workingArea.Bottom - Height)));
    }

    // Egy beviteli mezőben az Enter csak a beírt értéket véglegesíti (nem menti a beállításokat); a
    // Ctrl+Enter bárhonnan ment. A többi vezérlőn az Enter az AcceptButton (Mentés) szerint működik.
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.Enter))
        {
            saveButton.PerformClick();
            return true;
        }

        if (keyData == Keys.Enter && ActiveControl is SettingsTab tab && tab.CommitFocusedInput())
        {
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    // Egy felhasználói művelet több vezérlőeseményt is kivált (a számmezőnél az érték és a szöveg, a
    // legördülő listánál a kiválasztás és a szöveg változását), ezért a presenter nem eseményenként,
    // hanem az üzenetsor következő körében, egyszer kap jelzést.
    private void ScheduleSettingsEdited()
    {
        if (_editScheduled)
        {
            return;
        }

        if (!IsHandleCreated)
        {
            SettingsEdited?.Invoke(this, EventArgs.Empty);
            return;
        }

        _editScheduled = true;
        BeginInvoke(() =>
        {
            if (_editScheduled && !IsDisposed)
            {
                _editScheduled = false;
                SettingsEdited?.Invoke(this, EventArgs.Empty);
            }
        });
    }

    // A szalag magassága a szöveghez igazodik, legfeljebb MaxBannerLines sorig; a hosszabb szöveg
    // görgethető. A végleges (DPI szerint méretezett) szélesség csak itt ismert. (A Visible itt nem
    // használható: amíg az ablak nem látszik, minden gyermeke rejtettnek számít.)
    private void LayoutBanner()
    {
        if (bannerTextBox.TextLength == 0)
        {
            return;
        }

        var textWidth = Math.Max(1, bannerTextBox.ClientSize.Width);
        var flags = TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPadding;
        var textHeight = TextRenderer.MeasureText(bannerTextBox.Text, bannerTextBox.Font, new Size(textWidth, int.MaxValue), flags).Height;
        var maxHeight = bannerTextBox.Font.Height * MaxBannerLines;
        bannerTextBox.ScrollBars = textHeight > maxHeight ? ScrollBars.Vertical : ScrollBars.None;
        var height = Math.Min(textHeight, maxHeight) + LogicalToDeviceUnits(4);
        bannerPanel.Height = height + bannerPanel.Padding.Vertical;
        rootLayout.PerformLayout();
    }

    // Az üzenetlista nem tartozik egyik fülhöz sem, ezért az üzenet végén a javítás helye (a fül) is
    // szerepel (SettingsIssue.ToString).
    private void ShowMessages(SettingsEditorViewState state)
    {
        var messages = new List<(string Text, Color Color)>();
        messages.AddRange(state.Errors.Select(error => (error.ToString(), ErrorColor)));
        messages.AddRange(state.Warnings.Select(warning => (warning.ToString(), WarningColor)));
        if (messages.Count == 0)
        {
            messages.Add(("A beállítások rendben vannak.", OkColor));
        }

        if (messages.SequenceEqual(_shownMessages))
        {
            return;
        }

        _shownMessages = messages;
        messagesTextBox.SuspendLayout();
        messagesTextBox.Clear();
        for (var i = 0; i < messages.Count; i++)
        {
            messagesTextBox.SelectionStart = messagesTextBox.TextLength;
            messagesTextBox.SelectionLength = 0;
            messagesTextBox.SelectionColor = messages[i].Color;
            messagesTextBox.AppendText((i > 0 ? Environment.NewLine : string.Empty) + "• " + messages[i].Text);
        }

        messagesTextBox.SelectionStart = 0;
        messagesTextBox.SelectionLength = 0;
        messagesTextBox.ResumeLayout();
    }

    // Az ingerek, a megjelenés és a szemmozgás alapértékei (a hardverbeállítások a géphez tartoznak).
    private void defaultsButton_Click(object? sender, EventArgs e) => DefaultsRequested?.Invoke(this, EventArgs.Empty);

    private void saveButton_Click(object? sender, EventArgs e) => SaveRequested?.Invoke(this, EventArgs.Empty);
}
