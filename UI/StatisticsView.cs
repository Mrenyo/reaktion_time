using ReactionTimeMeasurementApp.Measurement;

namespace ReactionTimeMeasurementApp.UI;

// A statisztika nézet (passzív nézet, lásd StatisticsPresenter): a mérések választólistája, a kiválasztott
// mérés próbái és az összesítés.
internal sealed partial class StatisticsView : UserControl, IStatisticsView
{
    private const int MaxVisibleRows = 10;
    private const int TrialColumnIndex = 0;
    private const int StatusColumnIndex = 4;

    private static readonly Color ValidColor = Color.FromArgb(112, 133, 62);
    private static readonly Color GridTextColor = Color.FromArgb(82, 82, 88);
    private static readonly Color GridSelectionColor = Color.FromArgb(252, 245, 236);
    private static readonly Color GridAlternatingRowColor = Color.FromArgb(252, 251, 250);

    // A nyers scanout-korrekció oszlop csak fejlesztői módban látszik; a végfelhasználó
    // (résztvevő) elől ezt a belső, mikroszekundumos debug-adatot el kell rejteni.
    private readonly bool _showScanoutDebugColumn;

    private readonly Font _gridHeaderFont = new("Segoe UI Semibold", 17F, FontStyle.Bold, GraphicsUnit.Point, 238);

    public event EventHandler? BackRequested;

    public event EventHandler<int>? SessionSelected;

    public StatisticsView(bool showScanoutDebugColumn)
    {
        _showScanoutDebugColumn = showScanoutDebugColumn;
        InitializeComponent();
        DoubleBuffered = true;
        Disposed += (_, _) => _gridHeaderFont.Dispose();
        ConfigureGridStyle();
        ConfigureScanoutDebugColumn();
        sessionSelectorComboBox.SelectedIndexChanged += (_, _) =>
        {
            if (sessionSelectorComboBox.SelectedIndex >= 0)
            {
                SessionSelected?.Invoke(this, sessionSelectorComboBox.SelectedIndex);
            }
        };
    }

    public void ApplyState(StatisticsViewState state)
    {
        if (IsDisposed)
        {
            return;
        }

        // A lista csak akkor épül újra, ha változott; a kiválasztott elemet a presenter adja (a presenter
        // a saját kiválasztására érkező eseményt figyelmen kívül hagyja).
        var combo = sessionSelectorComboBox;
        if (!combo.Items.Cast<string>().SequenceEqual(state.SessionEntries))
        {
            combo.BeginUpdate();
            combo.Items.Clear();
            combo.Items.AddRange([.. state.SessionEntries]);
            combo.EndUpdate();
        }

        combo.Enabled = state.SessionEntries.Count > 0;
        if (combo.SelectedIndex != state.SelectedIndex)
        {
            combo.SelectedIndex = state.SelectedIndex;
        }

        measurementTypeLabel.Text = state.HeaderText;
        summaryValueLabel.Text = state.SummaryText;
        totalTrialsLabel.Text = state.TotalText;

        resultsGrid.Rows.Clear();
        foreach (var trial in state.Rows)
        {
            object[] values = _showScanoutDebugColumn
                ? [trial.Trial, trial.EspTime, trial.LocalTime, trial.Difference, trial.Status, trial.ScanoutCorrection]
                : [trial.Trial, trial.EspTime, trial.LocalTime, trial.Difference, trial.Status];
            var row = resultsGrid.Rows[resultsGrid.Rows.Add(values)];
            row.Cells[TrialColumnIndex].Style.ForeColor = trial.Kind == StatisticsRowKind.Practice ? UiTheme.MutedText : UiTheme.Accent;
            row.Cells[StatusColumnIndex].Style.ForeColor = trial.Kind switch
            {
                StatisticsRowKind.Practice => UiTheme.MutedText,
                StatisticsRowKind.Valid => ValidColor,
                _ => UiTheme.Accent
            };
        }
    }

    private void ConfigureGridStyle()
    {
        resultsGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        resultsGrid.ColumnHeadersDefaultCellStyle.BackColor = UiTheme.Accent;
        resultsGrid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        resultsGrid.ColumnHeadersDefaultCellStyle.Font = _gridHeaderFont;
        resultsGrid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

        resultsGrid.DefaultCellStyle.BackColor = Color.White;
        resultsGrid.DefaultCellStyle.SelectionBackColor = GridSelectionColor;
        resultsGrid.DefaultCellStyle.SelectionForeColor = GridTextColor;
        resultsGrid.DefaultCellStyle.ForeColor = GridTextColor;
        resultsGrid.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

        resultsGrid.RowsDefaultCellStyle.BackColor = Color.White;
        resultsGrid.AlternatingRowsDefaultCellStyle.BackColor = GridAlternatingRowColor;

        // A táblázat magassága a fejléc és MaxVisibleRows sor; a többi sor görgethető.
        resultsGrid.Height = resultsGrid.ColumnHeadersHeight + (resultsGrid.RowTemplate.Height * MaxVisibleRows) + 2;
    }

    private void ConfigureScanoutDebugColumn()
    {
        if (!_showScanoutDebugColumn)
        {
            return;
        }

        resultsGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "scanoutCorrectionColumn",
            HeaderText = "Scanout-korrekció (µs)",
            FillWeight = 24F,
            ReadOnly = true,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
    }

    private void backButton_Click(object? sender, EventArgs e)
    {
        BackRequested?.Invoke(this, EventArgs.Empty);
    }
}
