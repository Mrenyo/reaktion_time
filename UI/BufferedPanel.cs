namespace ReactionTimeMeasurementApp.UI;

// Kettősen pufferelt panel, amely a rajzolásból kihagyja a gyermekeit (WS_CLIPCHILDREN): a teljes
// tartalma egy memóriabeli pufferbe rajzolódik, és egyetlen másolással kerül a képernyőre.
internal sealed class BufferedPanel : Panel
{
    private const int WS_CLIPCHILDREN = 0x02000000;
    private const int WS_CLIPSIBLINGS = 0x04000000;

    public BufferedPanel()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.UserPaint,
            true);

        UpdateStyles();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var createParams = base.CreateParams;
            createParams.Style |= WS_CLIPCHILDREN | WS_CLIPSIBLINGS;
            return createParams;
        }
    }
}
