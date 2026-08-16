namespace ReactionTimeMeasurementApp;

public partial class StatisticsView : UserControl
{
    private const int MaxVisibleRows = 10;

    public event EventHandler? BackRequested;

    public StatisticsView()
    {
        InitializeComponent();
        DoubleBuffered = true;
        ConfigureGridStyle();
        SetLatestSessionResult(null);
    }

    public void SetLatestSessionResult(MeasurementSessionResult? session)
    {
        resultsGrid.Rows.Clear();

        if (session is null)
        {
            measurementTypeLabel.Text = "Mérés típusa: még nincs adat";
            averageValueLabel.Text = "-";
            totalTrialsLabel.Text = "Próbák száma: 0 / 0";
            EnsureGridViewport();
            return;
        }

        measurementTypeLabel.Text = $"Mérés típusa: {session.ModeName}";
        averageValueLabel.Text = $"ESP: {FormatMs(session.AverageEspReactionTimeMs)} | Helyi: {FormatMs(session.AverageLocalReactionTimeMs)}";
        totalTrialsLabel.Text = $"Próbák száma: {session.Trials.Count} / {session.Trials.Count}";

        foreach (var trial in session.Trials)
        {
            var statusText = trial.Valid ? "Érvényes" : "Érvénytelen";
            var rowIndex = resultsGrid.Rows.Add(
                trial.TrialNumber.ToString(),
                FormatMs(trial.EspReactionTimeMs),
                FormatMs(trial.LocalReactionTimeMs),
                statusText);

            resultsGrid.Rows[rowIndex].Cells[0].Style.ForeColor = Color.FromArgb(255, 91, 24);
            resultsGrid.Rows[rowIndex].Cells[3].Style.ForeColor = trial.Valid
                ? Color.FromArgb(112, 133, 62)
                : Color.FromArgb(255, 91, 24);
        }

        EnsureGridViewport();
    }

    private static string FormatMs(double? value)
    {
        return value.HasValue ? $"{value.Value:0} ms" : "-";
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
}
