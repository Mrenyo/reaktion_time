using ReactionTimeMeasurementApp.Measurement;

namespace ReactionTimeMeasurementApp.UI;

public partial class CalibrationView : UserControl
{
    private readonly ICalibrationService _calibrationService;
    private Image? _calibrationImage;

    public event EventHandler? BackRequested;
    public event EventHandler? CalibrationCompleted;

    public CalibrationView(ICalibrationService calibrationService)
    {
        _calibrationService = calibrationService;
        _ = ViewBackgroundStyler.TryLoadBackground();
        InitializeComponent();
        DoubleBuffered = true;
        LoadCalibrationImage();
        ConfigureRoundedSurfaces();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var background = ViewBackgroundStyler.TryGetScaledBackground(ClientSize);
        if (background != null)
        {
            e.Graphics.DrawImageUnscaled(background, 0, 0);
            return;
        }

        using var brush = new SolidBrush(Color.FromArgb(248, 244, 243));
        e.Graphics.FillRectangle(brush, ClientRectangle);
    }

    private void backButton_Click(object? sender, EventArgs e)
    {
        BackRequested?.Invoke(this, EventArgs.Empty);
    }

    // Nem elég, hogy csatlakozik egy szemkövető: ellenőrizzük, hogy van-e rajta kalibráció, és
    // hogy ténylegesen érvényes tekintetadatot küld-e (lásd ICalibrationService.CheckExistingCalibrationAsync).
    private async void currentConfigurationButton_Click(object? sender, EventArgs e)
    {
        var originalText = currentConfigurationButton.Text;
        SetButtonsEnabled(false);
        currentConfigurationButton.Text = "✓ Kalibráció ellenőrzése...";

        try
        {
            var (result, validGazeRatio) = await _calibrationService.CheckExistingCalibrationAsync();
            switch (result)
            {
                case CalibrationCheckResult.Ok:
                    CalibrationCompleted?.Invoke(this, EventArgs.Empty);
                    BackRequested?.Invoke(this, EventArgs.Empty);
                    break;
                case CalibrationCheckResult.TrackerNotFound:
                    ErrorPopup.ShowTobiiNotConnected(this);
                    break;
                case CalibrationCheckResult.NoCalibrationData:
                    ErrorPopup.ShowCalibrationMissing(this);
                    break;
                case CalibrationCheckResult.NoValidGaze:
                    ErrorPopup.ShowGazeNotDetected(this, validGazeRatio);
                    break;
            }
        }
        catch (Exception ex)
        {
            ErrorPopup.ShowGeneral(this, ex);
        }
        finally
        {
            currentConfigurationButton.Text = originalText;
            SetButtonsEnabled(true);
        }
    }

    private async void recalibrationButton_Click(object? sender, EventArgs e)
    {
        SetButtonsEnabled(false);
        recalibrationButton.Text = "↻ Kalibráció folyamatban...";

        try
        {
            await _calibrationService.InitializeAsync(forceRefresh: true);
            var calibrationResult = await Task.Run(() => _calibrationService.RunUserCalibration());
            if (calibrationResult < 0)
            {
                ErrorPopup.ShowTobiiNotConnected(this);
                return;
            }

            CalibrationCompleted?.Invoke(this, EventArgs.Empty);
            BackRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ErrorPopup.ShowGeneral(this, ex);
        }
        finally
        {
            recalibrationButton.Text = "↻ Újrakalibrálás";
            SetButtonsEnabled(true);
        }
    }

    private void SetButtonsEnabled(bool enabled)
    {
        backButton.Enabled = enabled;
        currentConfigurationButton.Enabled = enabled;
        recalibrationButton.Enabled = enabled;
    }

    private void LoadCalibrationImage()
    {
        var assetPath = Path.Combine(AppContext.BaseDirectory, "Images", "calibration_page.png");
        var image = UiResourceCache.TryGetImage(assetPath);
        if (image is null)
        {
            calibrationPictureBox.Visible = false;
            return;
        }

        _calibrationImage = image;
        calibrationPictureBox.Image = _calibrationImage;
        calibrationPictureBox.Visible = true;
    }

    private void ConfigureRoundedSurfaces()
    {
        foreach (var button in new[] { backButton, recalibrationButton, currentConfigurationButton })
        {
            button.KeepRoundedCorners(18);
        }
    }
}
