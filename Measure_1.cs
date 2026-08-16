using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.IO;
using ReactionTimeMeasurementApp.Classes;

namespace ReactionTimeMeasurementApp;

public partial class Measure_1 : UserControl
{
    private const int TotalTrials = 10;
    private const int MinStimulusDelayMs = 350;
    private const int MaxStimulusDelayMs = 1100;
    private const float ViewingDistanceCm = 60f;
    private const float StimulusVisualAngleDeg = 1f;
    private const float StimulusSafeMarginPx = 36f;
    private const float FixationCrossSizePx = 18f;
    private const float FixationCrossThicknessPx = 3f;
    private const int SettlementPointThreshold = 160;

    private const string SerialPortName = "COM9";
    private const int SerialBaudRate = 9600;
    private static readonly TimeSpan ReactionTimeout = TimeSpan.FromSeconds(3);
    private static readonly Color PhotodiodeIdleColor = Color.Black;
    private static readonly Color PhotodiodeActiveColor = Color.White;

    private readonly Random _random = new();
    private readonly Stopwatch _reactionStopwatch = new();
    private readonly List<MeasurementResult> _currentResults = new();
    private readonly TobiiService tobiiService;
    private readonly bool _manageTrackingLifecycle;
    private readonly Panel _photodiodeBox = new();

    private RectangleF _stimulusRect = RectangleF.Empty;
    private bool _isMeasurementRunning;
    private bool _stimulusVisible;
    private int _completedTrials;
    private bool _tobiiInitialized;

    private Serial? _serial;
    private CancellationTokenSource? _measurementCts;
    private Task? _measurementTask;
    private TaskCompletionSource<double>? _localReactionTcs;
    private bool _serialAvailable;
    private bool _isTransitioning;

    public event EventHandler? BackRequested;
    public event EventHandler<MeasurementSessionResultEventArgs>? MeasurementSessionCompleted;

    internal Measure_1(TobiiService tobiiService, bool manageTrackingLifecycle)
    {
        InitializeComponent();

        this.tobiiService = tobiiService;
        _manageTrackingLifecycle = manageTrackingLifecycle;

        DoubleBuffered = true;
        ResizeRedraw = true;
        instructionLabel.Visible = false;
        statusFlowPanel.Visible = false;
        ConfigurePhotodiodeBox();
        UpdateUiState();
    }

    private void stagePanel_Paint(object? sender, PaintEventArgs e)
    {
        DrawScene(e.Graphics);
    }

    private void stagePanel_MouseClick(object? sender, MouseEventArgs e)
    {
        if (!_serialAvailable)
        {
            TryCaptureLocalReaction();
        }
    }

    private async void startMeasureButton_Click(object? sender, EventArgs e)
    {
        if (_isTransitioning)
        {
            return;
        }

        _isTransitioning = true;
        UpdateUiState();

        try
        {
            await StartMeasurementAsync();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            ErrorPopup.ShowGeneral(this, ex);
            _isMeasurementRunning = false;
        }
        finally
        {
            _isTransitioning = false;
            UpdateUiState();
        }
    }

    private void backButton_Click(object? sender, EventArgs e)
    {
        if (_isTransitioning)
        {
            return;
        }

        _isTransitioning = true;
        UpdateUiState();
        BackRequested?.Invoke(this, EventArgs.Empty);
        BeginInvoke(new Action(StopMeasurementAfterNavigationAsync));
    }

    private async void StopMeasurementAfterNavigationAsync()
    {
        try
        {
            await StopMeasurementAsync();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            ErrorPopup.ShowGeneral(this, ex);
        }
        finally
        {
            _isTransitioning = false;
            UpdateUiState();
        }
    }

    private async Task StartMeasurementAsync()
    {
        await StopMeasurementAsync();
        if (!EnsureTobiiReady())
        {
            return;
        }

        if (_manageTrackingLifecycle)
        {
            tobiiService.StartTracking();
        }

        _currentResults.Clear();
        _completedTrials = 0;
        _isMeasurementRunning = true;
        _stimulusVisible = false;
        _stimulusRect = RectangleF.Empty;
        _reactionStopwatch.Reset();
        _localReactionTcs = null;
        _serialAvailable = false;
        SetPhotodiodeActive(false);
        UpdateUiState();
        stagePanel.Invalidate();

        _measurementCts = new CancellationTokenSource();
        var cancellationToken = _measurementCts.Token;

        try
        {
            _serial = new Serial(SerialPortName, SerialBaudRate);
            await _serial.StartAsync(CancellationToken.None).ConfigureAwait(false);
            _serialAvailable = true;
            Functions.Debug($"[SERIAL] Port open: {SerialPortName} @ {SerialBaudRate}");
        }
        catch (Exception ex)
        {
            Functions.Debug($"[SERIAL] Port open failed: {ex.Message}");
            await RunOnUiThreadAsync(() =>
                ErrorPopup.ShowEspNotConnected(this, SerialPortName, ex)).ConfigureAwait(false);
            await DisposeSerialAsync().ConfigureAwait(false);
            _serialAvailable = false;
            await RunOnUiThreadAsync(() =>
            {
                _isMeasurementRunning = false;
                UpdateUiState();
            }).ConfigureAwait(false);
            return;
        }

        _measurementTask = RunMeasurementLoopAsync(cancellationToken);
    }

    private async Task RunMeasurementLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && _completedTrials < TotalTrials)
            {
                var trialNumber = _completedTrials + 1;
                var scanoutParameters = await PrepareScanoutTrialAsync(trialNumber).ConfigureAwait(false);
                var scanoutCorrectionMessage = scanoutParameters.CreateCorrectionMessage();
                Functions.Debug(
                    $"[SCANOUT] Trial {trialNumber}: correctionUs={scanoutCorrectionMessage}");

                double? espReactionTimeMs;
                double? localReactionTimeMs;
                if (_serialAvailable && _serial is not null)
                {
                    try
                    {
                        await _serial.SendAsync(scanoutCorrectionMessage, cancellationToken).ConfigureAwait(false);
                        await ShowStimulusAfterRandomDelayAsync(trialNumber, cancellationToken).ConfigureAwait(false);
                        (espReactionTimeMs, localReactionTimeMs) = await WaitForEspReactionAsync(_serial, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is UnauthorizedAccessException || ex is InvalidOperationException || ex is IOException)
                    {
                        Functions.Debug($"[SERIAL] Runtime error: {ex.Message}");
                        _serialAvailable = false;
                        await DisposeSerialAsync().ConfigureAwait(false);
                        await RunOnUiThreadAsync(() =>
                            ErrorPopup.ShowEspNotConnected(this, SerialPortName, ex)).ConfigureAwait(false);
                        break;
                    }
                }
                else
                {
                    espReactionTimeMs = null;
                    await ShowStimulusAfterRandomDelayAsync(trialNumber, cancellationToken).ConfigureAwait(false);
                    localReactionTimeMs = await WaitForLocalReactionAsync(cancellationToken).ConfigureAwait(false);
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                await RunOnUiThreadAsync(() => RecordReaction(espReactionTimeMs, localReactionTimeMs)).ConfigureAwait(false);
            }

            if (_completedTrials >= TotalTrials)
            {
                await RunOnUiThreadAsync(FinishMeasurement).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            await RunOnUiThreadAsync(() => ErrorPopup.ShowGeneral(this, ex)).ConfigureAwait(false);
        }
        finally
        {
            await DisposeSerialAsync().ConfigureAwait(false);

            if (_manageTrackingLifecycle)
            {
                tobiiService.StopTracking();
            }

            await RunOnUiThreadAsync(() =>
            {
                _isMeasurementRunning = false;
                _stimulusVisible = false;
                _stimulusRect = RectangleF.Empty;
                _reactionStopwatch.Reset();
                _localReactionTcs?.TrySetCanceled();
                _localReactionTcs = null;
                SetPhotodiodeActive(false);
                UpdateUiState();
                stagePanel.Invalidate();
            }).ConfigureAwait(false);
        }
    }

    private async Task<ScanoutTrialParameters> PrepareScanoutTrialAsync(
        int trialNumber)
    {
        ScanoutTrialParameters? parameters = null;
        await RunOnUiThreadAsync(() =>
        {
            PrepareStimulus();
            parameters = DisplayScanoutContext.Capture(stagePanel, _stimulusRect, _photodiodeBox);
            instructionLabel.Text = $"Próba {trialNumber}/{TotalTrials}: a mérési paraméterek elküldése az ESP-nek.";
            stagePanel.Invalidate();
        }).ConfigureAwait(false);

        return parameters
            ?? throw new InvalidOperationException("Nem sikerült előkészíteni a scanout mérési paramétereket.");
    }

    private async Task ShowStimulusAfterRandomDelayAsync(int trialNumber, CancellationToken cancellationToken)
    {
        await RunOnUiThreadAsync(() =>
        {
            _stimulusVisible = false;
            _reactionStopwatch.Reset();
            _localReactionTcs = null;
            SetPhotodiodeActive(false);
            instructionLabel.Text = $"Próba {trialNumber}/{TotalTrials}: várakozás a következő pontra.";
            stagePanel.Invalidate();
        }).ConfigureAwait(false);

        int waitingTime;
        lock (_random)
        {
            waitingTime = _random.Next(MinStimulusDelayMs, MaxStimulusDelayMs + 1);
        }

        await Task.Delay(waitingTime, cancellationToken).ConfigureAwait(false);

        await RunOnUiThreadAsync(() =>
        {
            ShowStimulus();
            stagePanel.Invalidate();
        }).ConfigureAwait(false);
    }

    private void ShowStimulus()
    {
        if (_stimulusRect.Width <= 0 || _stimulusRect.Height <= 0)
        {
            throw new InvalidOperationException("Az inger pozíciója nincs előkészítve.");
        }

        _stimulusVisible = true;
        _reactionStopwatch.Restart();
        _localReactionTcs = new TaskCompletionSource<double>(TaskCreationOptions.RunContinuationsAsynchronously);
        SetPhotodiodeActive(true);
        instructionLabel.Text = _serialAvailable
            ? $"Próba {_completedTrials + 1}/{TotalTrials}: az inger megjelent, várakozás az ESP kattintási üzenetére."
            : $"Próba {_completedTrials + 1}/{TotalTrials}: az inger megjelent, kattints a reakció rögzítéséhez.";
    }

    private void PrepareStimulus()
    {
        var diameter = VisualAngleToPixels(StimulusVisualAngleDeg);
        var availableWidth = Math.Max(diameter, stagePanel.ClientSize.Width - (2 * StimulusSafeMarginPx));
        var availableHeight = Math.Max(diameter, stagePanel.ClientSize.Height - (2 * StimulusSafeMarginPx));
        var x = StimulusSafeMarginPx + (float)_random.NextDouble() * Math.Max(1f, availableWidth - diameter);
        var y = StimulusSafeMarginPx + (float)_random.NextDouble() * Math.Max(1f, availableHeight - diameter);
        _stimulusRect = new RectangleF(x, y, diameter, diameter);
        _stimulusVisible = false;
    }

    private async Task<(double? espReactionTimeMs, double? localReactionTimeMs)> WaitForEspReactionAsync(Serial serial, CancellationToken cancellationToken)
    {
        var localReactionTimeMs = _reactionStopwatch.Elapsed.TotalMilliseconds;

        try
        {
            var reactionResponse = await serial.WaitForResponseAsync(
                message => !string.IsNullOrWhiteSpace(message),
                ReactionTimeout,
                cancellationToken).ConfigureAwait(false);

            localReactionTimeMs = _reactionStopwatch.Elapsed.TotalMilliseconds;
            _reactionStopwatch.Stop();
            Functions.Debug($"[SERIAL][RAW] '{reactionResponse}'");

            if (string.IsNullOrWhiteSpace(reactionResponse))
            {
                throw new IOException("Az ESP nem küldött reakcióidő-adatot.");
            }

            if (!ScanoutProtocol.TryParseReactionTime(reactionResponse, out var espReactionTimeMs))
            {
                throw new FormatException($"Az ESP hibás reakcióidő-adatot küldött: {reactionResponse}");
            }

            Functions.Debug($"[SERIAL][PARSED] ESP reaction = {espReactionTimeMs:0.###} ms.");
            return (espReactionTimeMs, localReactionTimeMs);
        }
        catch (TimeoutException ex)
        {
            _reactionStopwatch.Stop();
            throw new IOException("Az ESP válaszideje túllépte a megengedett időt.", ex);
        }
        catch (OperationCanceledException)
        {
            _reactionStopwatch.Stop();
            return (null, null);
        }
    }

    private void RecordReaction(double? espReactionTimeMs, double? localReactionTimeMs)
    {
        if (!_isMeasurementRunning)
        {
            return;
        }

        _reactionStopwatch.Reset();
        _stimulusVisible = false;
        SetPhotodiodeActive(false);
        _completedTrials++;

        var x1 = stagePanel.ClientSize.Width / 2;
        var y1 = stagePanel.ClientSize.Height / 2;
        Rectangle stagePanelScreenBounds = stagePanel.RectangleToScreen(stagePanel.ClientRectangle);

        Point screenPoint = tobiiService.GetGazePoint();
        Point gazePoint = new(
            screenPoint.X - stagePanelScreenBounds.Left,
            screenPoint.Y - stagePanelScreenBounds.Top);

        bool inside = tobiiService.IsTrackerAvailable && Functions.IsInside(SettlementPointThreshold, x1, y1, gazePoint.X, gazePoint.Y);

        var result = new MeasurementResult(_completedTrials, TotalTrials, espReactionTimeMs, localReactionTimeMs, DateTime.Now, inside);
        _currentResults.Add(result);

        statusValueLabel.Text = $"{_completedTrials}/{TotalTrials}";
        lastResultValueLabel.Text = $"ESP {FormatReaction(espReactionTimeMs)} | PC {FormatReaction(localReactionTimeMs)}";
        instructionLabel.Text = $"Próba {_completedTrials}/{TotalTrials}: ESP {FormatReaction(espReactionTimeMs)}, PC {FormatReaction(localReactionTimeMs)}.";
        stagePanel.Invalidate();
    }

    private bool TryCaptureLocalReaction()
    {
        if (!_isMeasurementRunning || !_stimulusVisible)
        {
            return false;
        }

        var localReactionTcs = _localReactionTcs;
        if (localReactionTcs is null)
        {
            return false;
        }

        var localReactionTimeMs = _reactionStopwatch.Elapsed.TotalMilliseconds;
        if (!localReactionTcs.TrySetResult(localReactionTimeMs))
        {
            return false;
        }

        _reactionStopwatch.Stop();
        _stimulusVisible = false;
        SetPhotodiodeActive(false);
        stagePanel.Invalidate();
        return true;
    }

    private async Task<double?> WaitForLocalReactionAsync(CancellationToken cancellationToken)
    {
        var localReactionTcs = _localReactionTcs ?? throw new InvalidOperationException("A helyi reakcióidő-mérés nincs előkészítve.");
        var timeoutTask = Task.Delay(ReactionTimeout, cancellationToken);
        var completedTask = await Task.WhenAny(localReactionTcs.Task, timeoutTask).ConfigureAwait(false);

        if (completedTask == localReactionTcs.Task)
        {
            if (localReactionTcs.Task.IsCanceled)
            {
                return null;
            }

            return await localReactionTcs.Task.ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return null;
    }

    private static string FormatReaction(double? value)
    {
        return value.HasValue ? $"{value.Value:0} ms" : "-";
    }

    private void FinishMeasurement()
    {
        _isMeasurementRunning = false;
        _stimulusVisible = false;
        _stimulusRect = RectangleF.Empty;
        _reactionStopwatch.Reset();
        SetPhotodiodeActive(false);
        startMeasureButton.Enabled = true;
        startMeasureButton.Text = "Új mérés indítása";
        instructionLabel.Text = "A 10 próba elkészült. Indíthatsz új mérést vagy visszaléphetsz a menübe.";

        var session = new MeasurementSessionResult("Mérés 1", _currentResults.ToArray(), DateTime.Now);
        MeasurementSessionCompleted?.Invoke(this, new MeasurementSessionResultEventArgs(session));
        stagePanel.Invalidate();
    }

    private async Task StopMeasurementAsync()
    {
        _measurementCts?.Cancel();

        if (_measurementTask is not null)
        {
            try
            {
                await _measurementTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }

            _measurementTask = null;
        }

        _measurementCts?.Dispose();
        _measurementCts = null;
        _localReactionTcs?.TrySetCanceled();
        _localReactionTcs = null;
        _serialAvailable = false;

        await DisposeSerialAsync().ConfigureAwait(false);

        if (_manageTrackingLifecycle)
        {
            tobiiService.StopTracking();
        }

        await RunOnUiThreadAsync(() =>
        {
            _isMeasurementRunning = false;
            _stimulusVisible = false;
            _stimulusRect = RectangleF.Empty;
            _reactionStopwatch.Reset();
            SetPhotodiodeActive(false);
            UpdateUiState();
            stagePanel.Invalidate();
        }).ConfigureAwait(false);
    }

    private async Task DisposeSerialAsync()
    {
        var serial = Interlocked.Exchange(ref _serial, null);
        if (serial is null)
        {
            return;
        }

        try
        {
            await serial.StopAsync().ConfigureAwait(false);
        }
        catch
        {
        }

        await serial.DisposeAsync().ConfigureAwait(false);
    }

    private void UpdateUiState()
    {
        instructionLabel.Visible = false;
        statusFlowPanel.Visible = false;
        backButton.Enabled = !_isTransitioning;

        if (!_isMeasurementRunning)
        {
            startMeasureButton.Enabled = !_isTransitioning;
            startMeasureButton.Text = "Mérés indítása";
            statusValueLabel.Text = $"{_completedTrials}/{TotalTrials}";
            instructionLabel.Text = "A pont megjelenése után az ESP elküldi a reakcióidőt; a PC külön méri a beérkezés idejét.";
        }
        else
        {
            startMeasureButton.Enabled = false;
            startMeasureButton.Text = _isTransitioning ? "Leállítás..." : "Mérés folyamatban";
        }
    }

    private void DrawScene(Graphics graphics)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.FromArgb(30, 30, 30));

        var center = new PointF(stagePanel.ClientSize.Width / 2f, stagePanel.ClientSize.Height / 2f);
        DrawFixationCross(graphics, center);

        if (_stimulusVisible)
        {
            using var brush = new SolidBrush(Color.White);
            graphics.FillEllipse(brush, _stimulusRect);
        }
    }

    private static void DrawFixationCross(Graphics graphics, PointF center)
    {
        using var pen = new Pen(Color.White, FixationCrossThicknessPx)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        var half = FixationCrossSizePx / 2f;
        graphics.DrawLine(pen, center.X - half, center.Y, center.X + half, center.Y);
        graphics.DrawLine(pen, center.X, center.Y - half, center.X, center.Y + half);
    }

    private float VisualAngleToPixels(float angleInDegrees)
    {
        var dpi = DeviceDpi > 0 ? DeviceDpi : 96f;
        var angleInRadians = (MathF.PI / 180f) * angleInDegrees;
        var sizeCm = 2f * ViewingDistanceCm * MathF.Tan(angleInRadians / 2f);
        var sizeInches = sizeCm / 2.54f;

        return MathF.Max(12f, sizeInches * dpi);
    }

    private bool EnsureTobiiReady()
    {
        if (!_tobiiInitialized || !tobiiService.IsTrackerAvailable)
        {
            tobiiService.Initialize(forceRefresh: true);
            _tobiiInitialized = true;
        }

        if (tobiiService.IsTrackerAvailable)
        {
            return true;
        }

        ErrorPopup.ShowTobiiNotConnected(this);
        return false;
    }

    private void ConfigurePhotodiodeBox()
    {
        _photodiodeBox.Size = new Size(56, 56);
        _photodiodeBox.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
        _photodiodeBox.Location = new Point(Math.Max(0, Width - _photodiodeBox.Width - 20), Math.Max(0, Height - 84));
        _photodiodeBox.BorderStyle = BorderStyle.FixedSingle;
        _photodiodeBox.BackColor = PhotodiodeIdleColor;
        _photodiodeBox.TabStop = false;
        Controls.Add(_photodiodeBox);
        _photodiodeBox.BringToFront();

        Resize += (_, _) =>
        {
            _photodiodeBox.Location = new Point(Math.Max(0, Width - _photodiodeBox.Width - 20), Math.Max(0, Height - 84));
        };
    }

    private void SetPhotodiodeActive(bool active)
    {
        var targetColor = active ? PhotodiodeActiveColor : PhotodiodeIdleColor;

        if (_photodiodeBox.InvokeRequired)
        {
            _photodiodeBox.BeginInvoke(() => _photodiodeBox.BackColor = targetColor);
            return;
        }

        _photodiodeBox.BackColor = targetColor;
    }

    private Task RunOnUiThreadAsync(Action action)
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

        var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        BeginInvoke(new Action(() =>
        {
            try
            {
                action();
                tcs.SetResult(null);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }));

        return tcs.Task;
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        _measurementCts?.Cancel();
        base.OnHandleDestroyed(e);
    }
}







