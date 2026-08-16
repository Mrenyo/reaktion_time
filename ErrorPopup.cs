namespace ReactionTimeMeasurementApp;

internal static class ErrorPopup
{
    private const string GeneralTitle = "Hiba";

    internal static void ShowTobiiNotConnected(Control? owner)
    {
        Show(
            owner,
            "A Tobii szemkövető nem érhető el. Ellenőrizd, hogy az eszköz csatlakoztatva van-e, majd próbáld újra.",
            "Tobii kapcsolati hiba",
            MessageBoxIcon.Error);
    }

    internal static void ShowCalibrationRequired(Control? owner)
    {
        Show(
            owner,
            "A mérés indításához először válassz ki vagy végezz el egy kalibrációt.",
            "Kalibráció szükséges",
            MessageBoxIcon.Warning);
    }

    internal static void ShowEspNotConnected(Control? owner, string portName, Exception? exception = null)
    {
        var message = $"Az ESP nem érhető el a(z) {portName} porton. Ellenőrizd a csatlakozást, majd próbáld újra.";
        Show(owner, AppendDetails(message, exception), "ESP kapcsolati hiba", MessageBoxIcon.Error);
    }

    internal static void ShowGeneral(Control? owner, Exception? exception = null)
    {
        const string message = "Váratlan hiba történt. Próbáld újra, vagy indítsd újra az alkalmazást.";
        Show(owner, AppendDetails(message, exception), GeneralTitle, MessageBoxIcon.Error);
    }

    private static string AppendDetails(string message, Exception? exception)
    {
        return exception is null || string.IsNullOrWhiteSpace(exception.Message)
            ? message
            : $"{message}{Environment.NewLine}{Environment.NewLine}Részletek: {exception.Message}";
    }

    private static void Show(Control? owner, string message, string title, MessageBoxIcon icon)
    {
        if (owner is { IsDisposed: false, IsHandleCreated: true } && owner.InvokeRequired)
        {
            owner.BeginInvoke(new Action(() => Show(owner, message, title, icon)));
            return;
        }

        var window = owner?.FindForm() ?? owner;
        if (window is not null)
        {
            MessageBox.Show(window, message, title, MessageBoxButtons.OK, icon);
            return;
        }

        MessageBox.Show(message, title, MessageBoxButtons.OK, icon);
    }
}
