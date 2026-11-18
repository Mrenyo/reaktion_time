using ReactionTimeMeasurementApp.Measurement;

namespace ReactionTimeMeasurementApp.UI;

// A kalibrációs nézet (passzív nézet, lásd CalibrationPresenter): a meglévő kalibráció megerősítése vagy
// új kalibráció.
internal sealed partial class CalibrationView : UserControl, ICalibrationView
{
    private readonly Dictionary<Button, string> _idleTexts = new();

    public event EventHandler? BackRequested;

    public event EventHandler<CalibrationAction>? ActionRequested;

    public CalibrationView()
    {
        InitializeComponent();
        DoubleBuffered = true;
        LoadCalibrationImage();

        foreach (var button in new[] { backButton, recalibrationButton, currentConfigurationButton })
        {
            button.KeepRoundedCorners(UiTheme.ButtonCornerRadius);
            _idleTexts[button] = button.Text;
        }
    }

    public void SetBusy(CalibrationAction? action)
    {
        if (IsDisposed)
        {
            return;
        }

        foreach (var (button, idleText) in _idleTexts)
        {
            button.Enabled = action is null;
            button.Text = idleText;
        }

        if (action == CalibrationAction.ConfirmExisting)
        {
            currentConfigurationButton.Text = "✓ Kalibráció ellenőrzése...";
        }
        else if (action == CalibrationAction.Recalibrate)
        {
            recalibrationButton.Text = "↻ Kalibráció folyamatban...";
        }
    }

    public void ShowTrackerNotConnected() => ErrorPopup.ShowTobiiNotConnected(this);

    public void ShowCalibrationMissing() => ErrorPopup.ShowCalibrationMissing(this);

    public void ShowGazeNotDetected(double validGazeRatio) => ErrorPopup.ShowGazeNotDetected(this, validGazeRatio);

    public void ShowEyeTrackerManagerMissing(Exception exception) => ErrorPopup.ShowEyeTrackerManagerMissing(this, exception);

    public void ShowCalibrationFailed(Exception exception) => ErrorPopup.ShowCalibrationFailed(this, exception);

    public void ShowUnexpectedError(Exception exception) => ErrorPopup.ShowGeneral(this, exception);

    protected override void OnPaintBackground(PaintEventArgs e) => ViewBackgroundStyler.DrawBackground(e.Graphics, ClientSize);

    private void backButton_Click(object? sender, EventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);

    private void currentConfigurationButton_Click(object? sender, EventArgs e) =>
        ActionRequested?.Invoke(this, CalibrationAction.ConfirmExisting);

    private void recalibrationButton_Click(object? sender, EventArgs e) =>
        ActionRequested?.Invoke(this, CalibrationAction.Recalibrate);

    private void LoadCalibrationImage()
    {
        var image = UiResourceCache.TryGetImage(Path.Combine(AppContext.BaseDirectory, "Images", "calibration_page.png"));
        calibrationPictureBox.Image = image;
        calibrationPictureBox.Visible = image is not null;
    }
}
