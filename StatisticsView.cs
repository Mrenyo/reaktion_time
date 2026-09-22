using ReactionTimeMeasurementApp.Classes;

namespace ReactionTimeMeasurementApp;

public partial class StatisticsView : UserControl
{
    private const int MaxVisibleRows = 10;

    // A nyers scanout-korrekció oszlop csak fejlesztői módban látszik (lásd DeveloperMode);
    // végfelhasználó (résztvevő) elől ezt a belső, mikroszekundumos debug-adatot el kell rejteni.
    private static bool ShowScanoutDebugColumn => DeveloperMode.IsEnabled;

    private IReadOnlyList<MeasurementSessionResult> _sessions = Array.Empty<MeasurementSessionResult>();

    public event EventHandler? BackRequested;

    public StatisticsView()
    {
        InitializeComponent();
        DoubleBuffered = true;
        ConfigureGridStyle();
        ConfigureScanoutDebugColumn();
        RefreshSessionList();
    }

    // A MainMenu minden mérés vége után ezt hívja meg. A mérések lemezre mentődnek (lásd
    // MeasurementResultStore), ezért ilyenkor a teljes, lemezen lévő előzményt újraolvassuk,
    // nem csak az imént befejezett munkamenetet jelenítjük meg.
    public void SetLatestSessionResult(MeasurementSessionResult? session)
    {
        RefreshSessionList(session);
    }

    private void RefreshSessionList(MeasurementSessionResult? fallbackSession = null)
    {
        var loaded = MeasurementResultStore.LoadAll();

        // Ha a lemezre írás valamiért meghiúsult, de van egy éppen befejezett session a
        // memóriában, azt inkább megjelenítjük, mintsem üres listát mutassunk.
        _sessions = loaded.Count > 0 || fallbackSession is null
            ? loaded
            : new[] { fallbackSession };

        sessionSelectorComboBox.SelectedIndexChanged -= SessionSelectorComboBox_SelectedIndexChanged;

        sessionSelectorComboBox.BeginUpdate();
        sessionSelectorComboBox.Items.Clear();
        foreach (var session in _sessions)
        {
            sessionSelectorComboBox.Items.Add(new SessionListEntry(session));
        }
        sessionSelectorComboBox.EndUpdate();

        sessionSelectorComboBox.Enabled = _sessions.Count > 0;

        if (_sessions.Count > 0)
        {
            // A lista mindig a legutóbbi méréssel kezdődik (MeasurementResultStore.LoadAll
            // dátum szerint csökkenő sorrendbe rendez), és alapértelmezetten ez jelenik meg.
            sessionSelectorComboBox.SelectedIndex = 0;
        }

        DisplaySession(_sessions.Count > 0 ? _sessions[0] : null);

        sessionSelectorComboBox.SelectedIndexChanged += SessionSelectorComboBox_SelectedIndexChanged;
    }

    private void SessionSelectorComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (sessionSelectorComboBox.SelectedItem is SessionListEntry entry)
        {
            DisplaySession(entry.Session);
        }
    }

    private void DisplaySession(MeasurementSessionResult? session)
    {
        resultsGrid.Rows.Clear();

        if (session is null)
        {
            measurementTypeLabel.Text = "Mérés típusa: még nincs adat";
            averageValueLabel.Text = "-";
            totalTrialsLabel.Text = "Érvényes: 0 / 0";
            EnsureGridViewport();
            return;
        }

        var statusSuffix = session.Status == SessionStatus.Completed ? string.Empty : $" – {session.Status.ToDisplayText()}";
        measurementTypeLabel.Text = $"Mérés típusa: {session.ModeName} ({session.StartedAt:yyyy-MM-dd HH:mm}){statusSuffix}";
        averageValueLabel.Text =
            $"ESP: {FormatSummary(session.EspSummary)}{Environment.NewLine}Helyi: {FormatSummary(session.LocalSummary)}";
        totalTrialsLabel.Text = $"Érvényes: {session.ValidTrialCount} / {session.TestTrialCount}";

        // A gyakorló próbák is látszanak ("Gy" előtaggal), de az összesítésbe nem számítanak.
        foreach (var trial in session.Trials)
        {
            var isPractice = trial.Phase == TrialPhase.Practice;
            var rowValues = new List<string>
            {
                isPractice ? $"Gy{trial.TrialNumber}" : trial.TrialNumber.ToString(),
                FormatMs(trial.EspReactionTimeMs),
                FormatMs(trial.LocalReactionTimeMs),
                isPractice ? "Gyakorló" : trial.InvalidReason.ToDisplayText()
            };

            if (ShowScanoutDebugColumn)
            {
                rowValues.Add(trial.ScanoutCorrectionUs ?? "-");
            }

            var rowIndex = resultsGrid.Rows.Add(rowValues.ToArray());

            resultsGrid.Rows[rowIndex].Cells[0].Style.ForeColor = isPractice ? Color.FromArgb(150, 150, 156) : Color.FromArgb(255, 91, 24);
            resultsGrid.Rows[rowIndex].Cells[3].Style.ForeColor = isPractice
                ? Color.FromArgb(150, 150, 156)
                : trial.Valid ? Color.FromArgb(112, 133, 62) : Color.FromArgb(255, 91, 24);
        }

        EnsureGridViewport();
    }

    private static string FormatMs(double? value)
    {
        return value.HasValue ? $"{value.Value:0} ms" : "-";
    }

    // Az összesítés csak az érvényes próbákból készül (lásd MeasurementSessionResult).
    private static string FormatSummary(ReactionTimeSummary? summary)
    {
        if (summary is null)
        {
            return "nincs érvényes próba";
        }

        var standardDeviation = summary.StandardDeviationMs.HasValue ? $"{summary.StandardDeviationMs.Value:0}" : "-";
        return $"átlag {summary.MeanMs:0} · medián {summary.MedianMs:0} · SD {standardDeviation} ms";
    }

    private void ConfigureGridStyle()
    {
        resultsGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        resultsGrid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(255, 91, 24);
        resultsGrid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        resultsGrid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 17F, FontStyle.Bold, GraphicsUnit.Point, 238);
        resultsGrid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

        resultsGrid.DefaultCellStyle.BackColor = Color.White;
        resultsGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(252, 245, 236);
        resultsGrid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(82, 82, 88);
        resultsGrid.DefaultCellStyle.ForeColor = Color.FromArgb(82, 82, 88);
        resultsGrid.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

        resultsGrid.RowsDefaultCellStyle.BackColor = Color.White;
        resultsGrid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(252, 251, 250);

        EnsureGridViewport();
    }

    private void ConfigureScanoutDebugColumn()
    {
        if (!ShowScanoutDebugColumn)
        {
            return;
        }

        // SCANOUT DEBUG: the UI addition is isolated in this method.
        resultsGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "scanoutCorrectionColumn",
            HeaderText = "Scanout -> ESP (µs)",
            FillWeight = 24F,
            ReadOnly = true,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
    }

    private void EnsureGridViewport()
    {
        var visibleRows = Math.Max(MaxVisibleRows, 1);
        var targetHeight = resultsGrid.ColumnHeadersHeight + (resultsGrid.RowTemplate.Height * visibleRows) + 2;
        resultsGrid.Height = targetHeight;
    }

    private void backButton_Click(object? sender, EventArgs e)
    {
        BackRequested?.Invoke(this, EventArgs.Empty);
    }

    private sealed record SessionListEntry(MeasurementSessionResult Session)
    {
        public override string ToString() =>
            $"{Session.StartedAt:yyyy-MM-dd HH:mm} – {Session.ModeName} ({Session.TestTrialCount} próba"
            + (Session.Status == SessionStatus.Completed ? ")" : $", {Session.Status.ToDisplayText()})");
    }
}
