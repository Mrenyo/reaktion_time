using Tobii.Research;

namespace ReactionTimeMeasurementApp.Classes;

internal class TobiiService
{
    private IEyeTracker? tracker;
    private bool _isInitialized;
    private bool _isTracking;
    private int _lastPointX;
    private int _lastPointY;

    internal bool IsTrackerAvailable => tracker != null;

    internal void Initialize(bool forceRefresh = false)
    {
        if (_isTracking || (_isInitialized && tracker != null && !forceRefresh))
        {
            return;
        }

        try
        {
            tracker = TryFindTracker();
            if (tracker != null)
            {
                Functions.Debug($"Device found: {tracker.Model}");
            }
            else
            {
                Functions.Debug("Tobii device is not connected. Measurements will be marked invalid.");
            }
        }
        catch (Exception e)
        {
            Functions.Debug($"Tobii error: {e}");
        }
        finally
        {
            _isInitialized = true;
        }
    }

    internal void StartTracking()
    {
        if (_isTracking)
        {
            return;
        }

        if (!_isInitialized)
        {
            Initialize();
        }

        if (tracker == null)
        {
            return;
        }

        tracker.GazeDataReceived += Tracker_GazeDataReceived;
        _isTracking = true;
    }

    internal void StopTracking()
    {
        if (!_isTracking)
        {
            return;
        }

        if (tracker == null)
        {
            return;
        }

        tracker.GazeDataReceived -= Tracker_GazeDataReceived;
        _isTracking = false;
    }

    internal int RunUserCalibration(int? screenNumber = null)
    {
        var eyeTracker = TryFindTracker();
        if (eyeTracker == null)
        {
            Functions.Debug("Calibration skipped: Tobii device is not connected.");
            return -1;
        }

        return TobiiCalibrationLauncher.RunUserCalibration(eyeTracker, screenNumber);
    }

    private IEyeTracker? TryFindTracker()
    {
        var trackers = EyeTrackingOperations.FindAllEyeTrackers().ToList();
        tracker = trackers.FirstOrDefault();
        return tracker;
    }

    private void Tracker_GazeDataReceived(object? sender, GazeDataEventArgs e)
    {
        var leftValid = e.LeftEye.GazePoint.Validity == Validity.Valid;
        var rightValid = e.RightEye.GazePoint.Validity == Validity.Valid;

        if (!leftValid && !rightValid)
        {
            return;
        }

        float x;
        float y;

        if (leftValid && rightValid)
        {
            var left = e.LeftEye.GazePoint.PositionOnDisplayArea;
            var right = e.RightEye.GazePoint.PositionOnDisplayArea;
            x = (left.X + right.X) / 2f;
            y = (left.Y + right.Y) / 2f;
        }
        else if (leftValid)
        {
            x = e.LeftEye.GazePoint.PositionOnDisplayArea.X;
            y = e.LeftEye.GazePoint.PositionOnDisplayArea.Y;
        }
        else
        {
            x = e.RightEye.GazePoint.PositionOnDisplayArea.X;
            y = e.RightEye.GazePoint.PositionOnDisplayArea.Y;
        }

        var screenBounds = Screen.PrimaryScreen?.Bounds ?? Rectangle.Empty;
        var pixelX = (int)(x * screenBounds.Width);
        var pixelY = (int)(y * screenBounds.Height);

        Interlocked.Exchange(ref _lastPointX, pixelX);
        Interlocked.Exchange(ref _lastPointY, pixelY);
    }

    internal Point GetGazePoint()
    {
        return new Point(
            Volatile.Read(ref _lastPointX),
            Volatile.Read(ref _lastPointY));
    }
}
