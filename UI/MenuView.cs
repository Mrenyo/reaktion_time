
namespace ReactionTimeMeasurementApp.UI;

// A főmenü: a kalibráció, a mérés (indítás és testreszabás) és a statisztika kártyája. Csak
// navigációs kéréseket jelez; a döntéseket (pl. kell-e kalibráció a méréshez) a MainForm hozza.
internal sealed partial class MenuView : UserControl
{
    private const int StatusLabelCornerRadius = 14;

    // A kártya képe és a fölötte, illetve alatta lévő elem közötti legkisebb távolság.
    private const int ImageSpacingPx = 24;

    private static readonly Color CalibratedBackColor = Color.FromArgb(230, 241, 224);
    private static readonly Color CalibratedForeColor = Color.FromArgb(118, 128, 82);
    private static readonly Color NotCalibratedBackColor = Color.FromArgb(249, 237, 211);
    private static readonly Color NotCalibratedForeColor = Color.FromArgb(168, 126, 45);

    public event EventHandler? OpenCalibrationRequested;
    public event EventHandler? OpenMeasureRequested;
    public event EventHandler? OpenSettingsRequested;
    public event EventHandler? OpenStatisticsRequested;

    public MenuView()
    {
        InitializeComponent();
        ConfigureOpaqueSurfaces();
        ConfigureCardImageLayout();
        ConfigureRoundedSurfaces();
        LoadMenuImages();
        SetCalibrationCompleted(false);
    }

    // Csak a kalibrációs kártya állapotcímkéjét frissíti.
    public void SetCalibrationCompleted(bool isCompleted)
    {
        calibrationStatusLabel.Text = isCompleted ? "● Kalibrált" : "● Nincs kalibrálva";
        calibrationStatusLabel.BackColor = isCompleted ? CalibratedBackColor : NotCalibratedBackColor;
        calibrationStatusLabel.ForeColor = isCompleted ? CalibratedForeColor : NotCalibratedForeColor;
    }

    protected override void OnPaintBackground(PaintEventArgs e) => ViewBackgroundStyler.DrawBackground(e.Graphics, ClientSize);

    // A kártyák és a rajtuk lévő elemek átlátszatlan hátteret kapnak, így rajzoláskor nem kell alájuk a
    // nézet háttérképét is kirajzolni.
    private void ConfigureOpaqueSurfaces()
    {
        foreach (var control in new Control[]
        {
            calibrationCard,
            calibrationTopLabel,
            calibrationImageBox,
            measureCard,
            measureTopLabel,
            measureImageBox,
            statisticsCard,
            statisticsTopLabel,
            statisticsImageBox
        })
        {
            control.BackColor = UiTheme.CardBackground;
        }
    }

    // A kártyák képe a cím és az alatta lévő elem (állapotcímke, illetve gomb) között középen áll.
    private void ConfigureCardImageLayout()
    {
        calibrationCard.Resize += (_, _) => CenterMenuImages();
        measureCard.Resize += (_, _) => CenterMenuImages();
        statisticsCard.Resize += (_, _) => CenterMenuImages();
        CenterMenuImages();
    }

    private void CenterMenuImages()
    {
        CenterMenuImage(calibrationCard, calibrationTopLabel, calibrationImageBox, calibrationStatusLabel.Top);
        CenterMenuImage(measureCard, measureTopLabel, measureImageBox, measureSettingsButton.Top);
        CenterMenuImage(statisticsCard, statisticsTopLabel, statisticsImageBox, statisticsActionButton.Top);
    }

    private static void CenterMenuImage(Panel card, Control title, PictureBox pictureBox, int bottomBoundary)
    {
        var topBoundary = title.Bottom + ImageSpacingPx;
        var availableHeight = Math.Max(0, bottomBoundary - ImageSpacingPx - topBoundary);
        var imageTop = topBoundary + Math.Max(0, (availableHeight - pictureBox.Height) / 2);
        var imageLeft = Math.Max(0, (card.ClientSize.Width - pictureBox.Width) / 2);
        pictureBox.Location = new Point(imageLeft, imageTop);
    }

    private void ConfigureRoundedSurfaces()
    {
        foreach (var panel in new[] { calibrationCard, measureCard, statisticsCard })
        {
            panel.KeepRoundedCorners(UiTheme.CardCornerRadius);
        }

        foreach (var button in new[] { calibrationActionButton, measureSettingsButton, measureStartButton, statisticsActionButton })
        {
            button.KeepRoundedCorners(UiTheme.ButtonCornerRadius);
        }

        calibrationStatusLabel.KeepRoundedCorners(StatusLabelCornerRadius);
    }

    private void LoadMenuImages()
    {
        LoadMenuImage(calibrationImageBox, "calibration.png");
        LoadMenuImage(measureImageBox, "measurement.png");
        LoadMenuImage(statisticsImageBox, "statistics.png");
    }

    // A képek a program mellé települnek (Images mappa); ha egy fájl mégis hiányzik, a kártya kép
    // nélkül jelenik meg. A képdoboz a közös (a UiResourceCache tulajdonában lévő) képet a saját
    // méretére igazítva rajzolja ki (SizeMode = Zoom).
    private static void LoadMenuImage(PictureBox pictureBox, string fileName)
    {
        pictureBox.Image = UiResourceCache.TryGetImage(Path.Combine(AppContext.BaseDirectory, "Images", fileName));
        pictureBox.Visible = pictureBox.Image is not null;
    }

    private void calibrationActionButton_Click(object? sender, EventArgs e) => OpenCalibrationRequested?.Invoke(this, EventArgs.Empty);

    private void statisticsActionButton_Click(object? sender, EventArgs e) => OpenStatisticsRequested?.Invoke(this, EventArgs.Empty);

    private void measureStartButton_Click(object? sender, EventArgs e) => OpenMeasureRequested?.Invoke(this, EventArgs.Empty);

    private void measureSettingsButton_Click(object? sender, EventArgs e) => OpenSettingsRequested?.Invoke(this, EventArgs.Empty);
}
