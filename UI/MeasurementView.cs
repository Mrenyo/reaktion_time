using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using ReactionTimeMeasurementApp.Measurement;

namespace ReactionTimeMeasurementApp.UI;

// A mérési nézet (MVP passzív nézet) mindkét mérési módhoz. Csak megjelenít (instrukció, haladás,
// fixációs kereszt, inger, fotodióda-jelző) és a felhasználói műveleteket eseményként továbbítja;
// a mérés menetét a MeasurementPresenter vezérli.
public partial class MeasurementView : UserControl, IMeasurementView
{
    private static readonly Color StageBackgroundColor = Color.FromArgb(30, 30, 30);
    private static readonly Color PhotodiodeIdleColor = Color.Black;
    private static readonly Color PhotodiodeActiveColor = Color.White;
    private static readonly Color PhotodiodeOutlineColor = Color.FromArgb(90, 90, 90);

    // Mérés közben a mérőfelület fölött nem látszik az egérmutató.
    private static readonly Lazy<Cursor> HiddenCursor = new(CreateHiddenCursor);

    private MeasurementViewState _state = new(
        IsRunning: false,
        IsBusy: false,
        StartButtonText: "Mérés indítása",
        InstructionText: string.Empty,
        ProgressText: string.Empty,
        LastResultText: "-",
        FixationCrossSizePx: 18f,
        FixationCrossThicknessPx: 3f);

    private RectangleF? _stimulusBounds;

    public MeasurementView()
    {
        InitializeComponent();
        DoubleBuffered = true;
        ResizeRedraw = true;

        // A mérőfelület dupla pufferelt: a teljes jelenet (háttér, kereszt, inger, fotodióda-jelző)
        // egy memóriabeli pufferbe rajzolódik, és egyetlen másolással kerül a képernyőre, így
        // félkész állapot (pl. inger már igen, jelző még nem) nem kerülhet egy képkockába.
        stagePanel.EnableDoubleBuffering();
    }

    public event EventHandler? StartRequested;

    public event EventHandler? LeaveRequested;

    public event EventHandler? AbortRequested;

    public event EventHandler? Closed;

    // Az Esc billentyűre (lásd MainMenu): csak futó mérés közben kér megszakítást.
    public bool TryRequestAbort()
    {
        if (!_state.IsRunning || _state.IsBusy)
        {
            return false;
        }

        AbortRequested?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public Task InvokeAsync(Action action)
    {
        if (IsDisposed)
        {
            return Task.CompletedTask;
        }

        if (!InvokeRequired)
        {
            action();
            return Task.CompletedTask;
        }

        var completion = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        BeginInvoke(new Action(() =>
        {
            try
            {
                action();
                completion.SetResult(null);
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        }));

        return completion.Task;
    }

    public StageInfo GetStageInfo()
    {
        var screen = Screen.FromControl(stagePanel);
        return new StageInfo(stagePanel.ClientSize, stagePanel.PointToScreen(Point.Empty), screen.Bounds, screen.DeviceName);
    }

    public void ApplyState(MeasurementViewState state)
    {
        _state = state;
        if (IsDisposed)
        {
            return;
        }

        backButton.Enabled = !state.IsBusy;
        startMeasureButton.Enabled = !state.IsRunning && !state.IsBusy;
        startMeasureButton.Text = state.StartButtonText;
        instructionLabel.Text = state.InstructionText;
        instructionLabel.Visible = !state.IsRunning;
        stagePanel.Cursor = state.IsRunning ? HiddenCursor.Value : Cursors.Default;
        statusValueLabel.Text = state.ProgressText;
        lastResultValueLabel.Text = state.LastResultText;
        stagePanel.Invalidate();
    }

    public void ShowStimulus(RectangleF? stimulusBounds, bool paintImmediately)
    {
        _stimulusBounds = stimulusBounds;
        if (IsDisposed)
        {
            return;
        }

        stagePanel.Invalidate();
        if (paintImmediately)
        {
            // Az Update() azonnal lefuttatja a rajzolást (nem várja meg, hogy a WM_PAINT sorra
            // kerüljön az üzenetsorban).
            stagePanel.Update();
        }
    }

    public void ShowTrackerNotConnected() => ErrorPopup.ShowTobiiNotConnected(this);

    public void ShowSettingsError(string settingsPath, string details) => ErrorPopup.ShowSettingsError(this, settingsPath, details);

    public void ShowSetupError(string details) => ErrorPopup.ShowMeasurementSetupError(this, details);

    public void ShowEspNotConnected(string portName, Exception exception) => ErrorPopup.ShowEspNotConnected(this, portName, exception);

    public void ShowEspCommunicationError(string portName, int baudRate, Exception exception) =>
        ErrorPopup.ShowEspCommunicationError(this, portName, baudRate, exception);

    public void ShowSaveFailed(string path, Exception exception) => ErrorPopup.ShowSaveFailed(this, path, exception);

    public void ShowUnexpectedError(Exception exception) => ErrorPopup.ShowGeneral(this, exception);

    protected override void OnHandleDestroyed(EventArgs e)
    {
        Closed?.Invoke(this, EventArgs.Empty);
        base.OnHandleDestroyed(e);
    }

    private void stagePanel_Paint(object? sender, PaintEventArgs e)
    {
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(StageBackgroundColor);

        DrawFixationCross(graphics, new PointF(stagePanel.ClientSize.Width / 2f, stagePanel.ClientSize.Height / 2f));

        if (_stimulusBounds is { } stimulus)
        {
            using var brush = new SolidBrush(Color.White);
            graphics.FillEllipse(brush, stimulus);
        }

        DrawPhotodiodeMarker(graphics);
    }

    private void DrawFixationCross(Graphics graphics, PointF center)
    {
        using var pen = new Pen(Color.White, _state.FixationCrossThicknessPx)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        var half = _state.FixationCrossSizePx / 2f;
        graphics.DrawLine(pen, center.X - half, center.Y, center.X + half, center.Y);
        graphics.DrawLine(pen, center.X, center.Y - half, center.X, center.Y + half);
    }

    // A jelző színét ugyanaz az állapot (_stimulusBounds) vezérli, mint az inger kirajzolását, és
    // ugyanabban a rajzolásban készül, így a kettő nem kerülhet külön képkockába.
    private void DrawPhotodiodeMarker(Graphics graphics)
    {
        var bounds = PhotodiodeMarker.GetBounds(stagePanel.ClientSize);
        if (bounds.IsEmpty)
        {
            return;
        }

        var previousSmoothingMode = graphics.SmoothingMode;
        graphics.SmoothingMode = SmoothingMode.None;

        using var brush = new SolidBrush(_stimulusBounds.HasValue ? PhotodiodeActiveColor : PhotodiodeIdleColor);
        graphics.FillRectangle(brush, bounds);

        // A vékony keret csak a fizikai szenzor felhelyezését segíti; a jelzőn kívül esik, és
        // sosem változik, ezért a fotodióda jelét nem befolyásolja.
        using var outlinePen = new Pen(PhotodiodeOutlineColor);
        graphics.DrawRectangle(outlinePen, bounds.X - 1, bounds.Y - 1, bounds.Width + 1, bounds.Height + 1);

        graphics.SmoothingMode = previousSmoothingMode;
    }

    private void startMeasureButton_Click(object? sender, EventArgs e) => StartRequested?.Invoke(this, EventArgs.Empty);

    private void backButton_Click(object? sender, EventArgs e) => LeaveRequested?.Invoke(this, EventArgs.Empty);

    private static Cursor CreateHiddenCursor()
    {
        using var bitmap = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
        return new Cursor(bitmap.GetHicon());
    }
}
