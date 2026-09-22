namespace ReactionTimeMeasurementApp;

public enum MeasureMode
{
    Normal,
    Peripheral
}

public sealed class MeasureModeRequestedEventArgs : EventArgs
{
    public MeasureModeRequestedEventArgs(MeasureMode mode)
    {
        Mode = mode;
    }

    public MeasureMode Mode { get; }
}
