using System.Drawing.Drawing2D;

namespace ReactionTimeMeasurementApp;

public partial class MenuView : UserControl
{
    private readonly Color _pageBackground = Color.FromArgb(248, 244, 243);
    private Bitmap? _generatedBackgroundCache;
    private readonly Dictionary<PictureBox, Image> _menuImageSources = new();
    private Size _generatedBackgroundCacheSize = Size.Empty;
    private bool _isCalibrationCompleted;
    private MeasureMode _selectedMeasureMode = MeasureMode.Normal;

    public event EventHandler? OpenCalibrationRequested;
    public event EventHandler<MeasureModeRequestedEventArgs>? OpenMeasureRequested;
    public event EventHandler? OpenStatisticsRequested;

    public MenuView()
    {
        InitializeComponent();
        DoubleBuffered = true;
        ConfigureOpaqueSurfaces();
        ConfigureNavigationCards();
        ConfigureRoundedSurfaces();
        LoadMenuImages();
        _ = ViewBackgroundStyler.TryLoadBackground();
        SetCalibrationCompleted(false);
        SelectMeasureMode(MeasureMode.Normal);
        SelectSection("Kalibráció");
        Resize += (_, _) =>
        {
            InvalidateGeneratedBackgroundCache();
            RefreshRoundedSurfaces();
        };
    }

    public void SetCalibrationCompleted(bool isCompleted)
    {
        _isCalibrationCompleted = isCompleted;
        UpdateCalibrationStatus();
        UpdateMeasureActionState();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var background = ViewBackgroundStyler.TryGetScaledBackground(ClientSize);
        if (background != null)
        {
            e.Graphics.DrawImageUnscaled(background, 0, 0);
            return;
        }

        EnsureGeneratedBackgroundCache();
        if (_generatedBackgroundCache != null)
        {
            e.Graphics.DrawImageUnscaled(_generatedBackgroundCache, 0, 0);
        }
    }

    private void ConfigureOpaqueSurfaces()
    {
        var cardBackColor = Color.FromArgb(253, 250, 250);

        calibrationCard.BackColor = cardBackColor;
        measureCard.BackColor = cardBackColor;
        statisticsCard.BackColor = cardBackColor;

        foreach (var control in new Control[]
        {
            calibrationTopLabel,
            calibrationIllustration,
            calibrationImageBox,
            calibrationStatusLabel,
            measureTopLabel,
            measureIllustration,
            measureImageBox,
            statisticsTopLabel,
            statisticsIllustration,
            statisticsImageBox
        })
        {
            control.BackColor = cardBackColor;
        }
    }
    private void ConfigureNavigationCards()
    {
        ConfigureCard(calibrationCard, calibrationTopLabel, calibrationIllustration, calibrationImageBox, calibrationStatusLabel);
        ConfigureCard(measureCard, measureTopLabel, measureIllustration, measureImageBox);
        ConfigureCard(statisticsCard, statisticsTopLabel, statisticsIllustration, statisticsImageBox);

        calibrationCard.Resize += (_, _) => CenterMenuImage(
            calibrationCard, calibrationTopLabel, calibrationImageBox, calibrationIllustration, calibrationStatusLabel.Top);
        measureCard.Resize += (_, _) => CenterMenuImage(
            measureCard, measureTopLabel, measureImageBox, measureIllustration, measureOptionPrimaryButton.Top);
        statisticsCard.Resize += (_, _) => CenterMenuImage(
            statisticsCard, statisticsTopLabel, statisticsImageBox, statisticsIllustration, statisticsActionButton.Top);

        CenterMenuImages();
    }

    private void CenterMenuImages()
    {
        CenterMenuImage(calibrationCard, calibrationTopLabel, calibrationImageBox, calibrationIllustration, calibrationStatusLabel.Top);
        CenterMenuImage(measureCard, measureTopLabel, measureImageBox, measureIllustration, measureOptionPrimaryButton.Top);
        CenterMenuImage(statisticsCard, statisticsTopLabel, statisticsImageBox, statisticsIllustration, statisticsActionButton.Top);
    }

    private static void CenterMenuImage(
        Panel card,
        Control title,
        PictureBox pictureBox,
        Control fallbackControl,
        int bottomBoundary)
    {
        const int spacing = 24;
        var topBoundary = title.Bottom + spacing;
        var availableHeight = Math.Max(0, bottomBoundary - spacing - topBoundary);
        var imageTop = topBoundary + Math.Max(0, (availableHeight - pictureBox.Height) / 2);
        var imageLeft = Math.Max(0, (card.ClientSize.Width - pictureBox.Width) / 2);
        var location = new Point(imageLeft, imageTop);

        pictureBox.Location = location;
        fallbackControl.Location = location;
    }

    private void ConfigureCard(Panel card, params Control[] clickableChildren)
    {
        card.Cursor = Cursors.Hand;
        card.Click += NavigationCard_Click;

        foreach (var control in clickableChildren)
        {
            control.Cursor = Cursors.Hand;
            control.Click += NavigationCard_Click;
        }
    }

    private void ConfigureRoundedSurfaces()
    {
        foreach (var panel in new[] { calibrationCard, measureCard, statisticsCard })
        {
            panel.Resize += (_, _) => ApplyRoundedRegion(panel, 28);
            ApplyRoundedRegion(panel, 28);
        }

        foreach (var button in new[] { calibrationActionButton, measureOptionPrimaryButton, measureOptionSecondaryButton, statisticsActionButton })
        {
            button.Resize += (_, _) => ApplyRoundedRegion(button, 16);
            ApplyRoundedRegion(button, 16);
        }

        calibrationStatusLabel.Resize += (_, _) => ApplyRoundedRegion(calibrationStatusLabel, 14);
        ApplyRoundedRegion(calibrationStatusLabel, 14);
    }

    private void LoadMenuImages()
    {
        LoadMenuImage(calibrationImageBox, calibrationIllustration, Path.Combine(AppContext.BaseDirectory, "Images", "calibration.png"));
        LoadMenuImage(measureImageBox, measureIllustration, Path.Combine(AppContext.BaseDirectory, "Images", "measurement.png"));
        LoadMenuImage(statisticsImageBox, statisticsIllustration, Path.Combine(AppContext.BaseDirectory, "Images", "statistiks.png"));
    }

    private void LoadMenuImage(PictureBox pictureBox, Control fallbackControl, string assetPath)
    {
        var sourceImage = UiResourceCache.TryGetImage(assetPath);
        if (sourceImage is null)
        {
            // Ha a képfájl hiányzik, a kódból rajzolt tartalék illusztráció jelenik meg helyette.
            pictureBox.Visible = false;
            fallbackControl.Visible = true;
            return;
        }

        _menuImageSources[pictureBox] = sourceImage;
        pictureBox.Resize += (_, _) => UpdateMenuImage(pictureBox);
        UpdateMenuImage(pictureBox);
        pictureBox.Visible = true;
        fallbackControl.Visible = false;
    }

    private void UpdateMenuImage(PictureBox pictureBox)
    {
        if (!_menuImageSources.TryGetValue(pictureBox, out var sourceImage)
            || pictureBox.ClientSize.Width <= 0
            || pictureBox.ClientSize.Height <= 0)
        {
            return;
        }

        var scaledImage = new Bitmap(pictureBox.ClientSize.Width, pictureBox.ClientSize.Height);
        using (var graphics = Graphics.FromImage(scaledImage))
        {
            graphics.CompositingQuality = CompositingQuality.HighSpeed;
            graphics.InterpolationMode = InterpolationMode.Low;
            graphics.PixelOffsetMode = PixelOffsetMode.HighSpeed;
            graphics.SmoothingMode = SmoothingMode.None;
            graphics.DrawImage(sourceImage, new Rectangle(Point.Empty, pictureBox.ClientSize));
        }

        var previousImage = pictureBox.Image;
        pictureBox.Image = scaledImage;

        if (previousImage is not null && !_menuImageSources.ContainsValue(previousImage))
        {
            previousImage.Dispose();
        }
    }

    private void EnsureGeneratedBackgroundCache()
    {
        if (ClientSize.Width <= 0 || ClientSize.Height <= 0)
        {
            return;
        }

        if (_generatedBackgroundCache != null && _generatedBackgroundCacheSize == ClientSize)
        {
            return;
        }

        _generatedBackgroundCache?.Dispose();
        _generatedBackgroundCacheSize = ClientSize;
        _generatedBackgroundCache = new Bitmap(ClientSize.Width, ClientSize.Height);

        using var graphics = Graphics.FromImage(_generatedBackgroundCache);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        using var backgroundBrush = new SolidBrush(_pageBackground);
        graphics.FillRectangle(backgroundBrush, new Rectangle(Point.Empty, _generatedBackgroundCacheSize));

        var width = _generatedBackgroundCacheSize.Width;
        var height = _generatedBackgroundCacheSize.Height;

        using var topGlow = new LinearGradientBrush(
            new PointF(0, 0),
            new PointF(width, height),
            Color.FromArgb(70, 255, 242, 236),
            Color.FromArgb(0, 255, 242, 236));
        graphics.FillEllipse(topGlow, -width * 0.1f, -height * 0.22f, width * 1.2f, height * 0.5f);

        using var leftGlow = new SolidBrush(Color.FromArgb(38, 255, 173, 130));
        graphics.FillEllipse(leftGlow, -width * 0.2f, height * 0.72f, width * 0.55f, height * 0.3f);

        using var rightGlow = new SolidBrush(Color.FromArgb(30, 255, 158, 137));
        graphics.FillEllipse(rightGlow, width * 0.68f, height * 0.68f, width * 0.42f, height * 0.26f);

        using var wavePen = new Pen(Color.FromArgb(26, 255, 196, 172), 2f);
        graphics.DrawBezier(
            wavePen,
            new PointF(-40, height - 60),
            new PointF(width * 0.18f, height - 140),
            new PointF(width * 0.34f, height - 8),
            new PointF(width * 0.55f, height - 70));
        graphics.DrawBezier(
            wavePen,
            new PointF(width * 0.45f, height - 38),
            new PointF(width * 0.62f, height - 124),
            new PointF(width * 0.78f, height - 8),
            new PointF(width + 30, height - 88));
    }

    private void InvalidateGeneratedBackgroundCache()
    {
        _generatedBackgroundCache?.Dispose();
        _generatedBackgroundCache = null;
        _generatedBackgroundCacheSize = Size.Empty;
    }

    private void RefreshRoundedSurfaces()
    {
        ApplyRoundedRegion(calibrationCard, 28);
        ApplyRoundedRegion(measureCard, 28);
        ApplyRoundedRegion(statisticsCard, 28);
        ApplyRoundedRegion(calibrationActionButton, 16);
        ApplyRoundedRegion(measureOptionPrimaryButton, 16);
        ApplyRoundedRegion(measureOptionSecondaryButton, 16);
        ApplyRoundedRegion(statisticsActionButton, 16);
        ApplyRoundedRegion(calibrationStatusLabel, 14);
    }

    private static void ApplyRoundedRegion(Control control, int radius)
    {
        if (control.Width <= 0 || control.Height <= 0)
        {
            return;
        }

        using var path = new GraphicsPath();
        var diameter = radius * 2;
        var rect = new Rectangle(0, 0, control.Width - 1, control.Height - 1);

        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();

        control.Region?.Dispose();
        control.Region = new Region(path);
    }

    private void NavigationCard_Click(object? sender, EventArgs e)
    {
        var control = sender as Control;
        var card = FindNavigationCard(control);

        if (card?.Tag is string sectionName)
        {
            SelectSection(sectionName);
        }
    }

    private void CalibrationActionButton_Click(object? sender, EventArgs e)
    {
        SelectSection("Kalibráció");
        OpenCalibrationRequested?.Invoke(this, EventArgs.Empty);
    }

    private void StatisticsActionButton_Click(object? sender, EventArgs e)
    {
        SelectSection("Statisztika");
        OpenStatisticsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void MeasureOptionPrimaryButton_Click(object? sender, EventArgs e)
    {
        SelectMeasureMode(MeasureMode.Normal);

        if (!EnsureCalibrationForMeasurement())
        {
            return;
        }

        OpenMeasureRequested?.Invoke(this, new MeasureModeRequestedEventArgs(_selectedMeasureMode));
    }

    private void MeasureOptionSecondaryButton_Click(object? sender, EventArgs e)
    {
        SelectMeasureMode(MeasureMode.Alternative);

        if (!EnsureCalibrationForMeasurement())
        {
            return;
        }

        OpenMeasureRequested?.Invoke(this, new MeasureModeRequestedEventArgs(_selectedMeasureMode));
    }

    private bool EnsureCalibrationForMeasurement()
    {
        SelectSection("Mérés");

        if (_isCalibrationCompleted)
        {
            return true;
        }

        ErrorPopup.ShowCalibrationRequired(this);
        return false;
    }

    private void SelectMeasureMode(MeasureMode mode)
    {
        _selectedMeasureMode = mode;
        UpdateMeasureOptionButtons();
    }

    private Control? FindNavigationCard(Control? control)
    {
        while (control is not null)
        {
            if (control is Panel && control.Tag is string)
            {
                return control;
            }

            control = control.Parent;
        }

        return null;
    }

    private void SelectSection(string sectionName)
    {
        ResetCardStyles();

        switch (sectionName)
        {
            case "Kalibráció":
                calibrationCard.BackColor = Color.FromArgb(255, 255, 255);
                calibrationTopLabel.ForeColor = Color.FromArgb(255, 91, 24);
                break;
            case "Mérés":
                measureCard.BackColor = Color.FromArgb(255, 255, 255);
                measureTopLabel.ForeColor = Color.FromArgb(255, 91, 24);
                break;
            case "Statisztika":
                statisticsCard.BackColor = Color.FromArgb(255, 255, 255);
                statisticsTopLabel.ForeColor = Color.FromArgb(255, 91, 24);
                break;
        }
    }

    private void UpdateMeasureActionState()
    {
        measureOptionPrimaryButton.Enabled = true;
        measureOptionSecondaryButton.Enabled = true;

        measureOptionPrimaryButton.Text = "Normál reakcióidő";
        measureOptionSecondaryButton.Text = "Periférikus reakcióidő";

        UpdateMeasureOptionButtons();
    }

    private void UpdateMeasureOptionButtons()
    {
        ApplyMeasureOptionStyle(measureOptionPrimaryButton, _isCalibrationCompleted);
        ApplyMeasureOptionStyle(measureOptionSecondaryButton, _isCalibrationCompleted);
    }

    private static void ApplyMeasureOptionStyle(Button button, bool enabled)
    {
        if (!enabled)
        {
            button.BackColor = Color.FromArgb(243, 237, 237);
            button.ForeColor = Color.FromArgb(160, 157, 165);
            return;
        }

        button.BackColor = Color.FromArgb(250, 245, 245);
        button.ForeColor = Color.FromArgb(85, 94, 114);
    }

    private void UpdateCalibrationStatus()
    {
        calibrationStatusLabel.Text = _isCalibrationCompleted ? "● Kalibrált" : "● Nincs kalibrálva";
        calibrationStatusLabel.BackColor = _isCalibrationCompleted
            ? Color.FromArgb(230, 241, 224)
            : Color.FromArgb(249, 237, 211);
        calibrationStatusLabel.ForeColor = _isCalibrationCompleted
            ? Color.FromArgb(118, 128, 82)
            : Color.FromArgb(168, 126, 45);
    }

    private void ResetCardStyles()
    {
        calibrationCard.BackColor = Color.FromArgb(253, 250, 250);
        measureCard.BackColor = Color.FromArgb(253, 250, 250);
        statisticsCard.BackColor = Color.FromArgb(253, 250, 250);

        calibrationTopLabel.ForeColor = Color.FromArgb(255, 91, 24);
        measureTopLabel.ForeColor = Color.FromArgb(255, 91, 24);
        statisticsTopLabel.ForeColor = Color.FromArgb(255, 91, 24);
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        InvalidateGeneratedBackgroundCache();
        DisposeScaledMenuImages();
        base.OnHandleDestroyed(e);
    }

    private void DisposeScaledMenuImages()
    {
        foreach (var pictureBox in _menuImageSources.Keys)
        {
            var image = pictureBox.Image;
            pictureBox.Image = null;

            if (image is not null && !_menuImageSources.ContainsValue(image))
            {
                image.Dispose();
            }
        }
    }
}







