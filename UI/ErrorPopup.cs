using ReactionTimeMeasurementApp.Diagnostics;

namespace ReactionTimeMeasurementApp.UI;

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

    internal static void ShowSettingsError(Control? owner, string settingsPath, string details)
    {
        Show(
            owner,
            $"A mérési beállítások hibásak, ezért a mérés nem indul el. Javítsd a fájlt, majd próbáld újra."
            + $"{Environment.NewLine}{Environment.NewLine}Fájl: {settingsPath}{Environment.NewLine}{Environment.NewLine}{details}",
            "Hibás mérési beállítások",
            MessageBoxIcon.Error);
    }

    internal static void ShowMeasurementSetupError(Control? owner, string details)
    {
        Show(owner, details, "A mérés nem indítható", MessageBoxIcon.Error);
    }

    internal static void ShowSaveFailed(Control? owner, string path, Exception exception)
    {
        Show(
            owner,
            AppendDetails(
                $"A mérési adatok mentése nem sikerült, ezért a mérés leáll (a már kiírt próbák megmaradnak).{Environment.NewLine}{Environment.NewLine}Hely: {path}",
                exception),
            "Mentési hiba",
            MessageBoxIcon.Error,
            exception);
    }

    internal static void ShowCalibrationMissing(Control? owner)
    {
        Show(
            owner,
            "A szemkövetőn nincs használható kalibráció. Végezz újrakalibrálást.",
            "Nincs kalibráció",
            MessageBoxIcon.Warning);
    }

    internal static void ShowGazeNotDetected(Control? owner, double validGazeRatio)
    {
        Show(
            owner,
            $"A szemkövető nem érzékel érvényes tekintetet (érvényes minták aránya: {validGazeRatio:P0}). "
            + "Ülj a szemkövető elé, nézz a képernyőre, majd próbáld újra. Ha így sem sikerül, végezz újrakalibrálást.",
            "Nem érzékelhető tekintet",
            MessageBoxIcon.Warning);
    }

    internal static void ShowEspNotConnected(Control? owner, string portName, Exception? exception = null)
    {
        if (IsPortBusyError(exception))
        {
            var busyMessage = $"A(z) {portName} port foglalt, ezért az alkalmazás nem tudja megnyitni. Zárd be a Serial Monitort, "
                + "a PlatformIO/Arduino monitort vagy a másik futó alkalmazáspéldányt, majd próbáld újra.";
            Show(owner, AppendDetails(busyMessage, exception), "COM-port foglalt", MessageBoxIcon.Error, exception);
            return;
        }

        var message = $"Az ESP nem érhető el a(z) {portName} porton. Ellenőrizd a csatlakozást, majd próbáld újra.";
        Show(owner, AppendDetails(message, exception), "ESP kapcsolati hiba", MessageBoxIcon.Error, exception);
    }

    internal static void ShowEspCommunicationError(Control? owner, string portName, int baudRate, Exception? exception = null)
    {
        var message = $"Az ESP a(z) {portName} porton megnyílt, de nem sikerült érvényes adatot váltani vele. Ellenőrizd, hogy az ESP "
            + $"firmware {baudRate} baudon fut-e, és a numerikus scanout protokollt használja-e.";
        Show(owner, AppendDetails(message, exception), "ESP kommunikációs hiba", MessageBoxIcon.Error, exception);
    }

    internal static void ShowGeneral(Control? owner, Exception? exception = null)
    {
        const string message = "Váratlan hiba történt. Próbáld újra, vagy indítsd újra az alkalmazást.";
        Show(owner, AppendDetails(message, exception), GeneralTitle, MessageBoxIcon.Error, exception);
    }

    private static bool IsPortBusyError(Exception? exception)
    {
        return exception is UnauthorizedAccessException
            || exception?.InnerException is UnauthorizedAccessException
            || exception?.Message.Contains("denied", StringComparison.OrdinalIgnoreCase) == true
            || exception?.Message.Contains("hozzáférés", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static string AppendDetails(string message, Exception? exception)
    {
        return exception is null || string.IsNullOrWhiteSpace(exception.Message)
            ? message
            : $"{message}{Environment.NewLine}{Environment.NewLine}Részletek: {exception.Message}";
    }

    private static void Show(Control? owner, string message, string title, MessageBoxIcon icon, Exception? exception = null)
    {
        // Minden felhasználó felé megjelenő hibaüzenetet itt naplózunk egy helyen, a teljes
        // kivétellel (stack trace-szel) együtt - így egy résztvevőnél tapasztalt hiba utólag,
        // a "logs" mappában is visszakereshető, nemcsak a felugró ablak rövid szövegében.
        // Ez a metódus csak egyszer fut le hívásonként; a UI-szálra váltást a ShowMessageBox
        // végzi, hogy az átirányítás miatt ne naplózzunk kétszer ugyanarról a hibáról.
        if (icon == MessageBoxIcon.Error)
        {
            Logger.Error($"[{title}] {message}", exception);
        }
        else
        {
            Logger.Warning($"[{title}] {message}");
        }

        ShowMessageBox(owner, message, title, icon);
    }

    private static void ShowMessageBox(Control? owner, string message, string title, MessageBoxIcon icon)
    {
        if (owner is { IsDisposed: false, IsHandleCreated: true } && owner.InvokeRequired)
        {
            owner.BeginInvoke(new Action(() => ShowMessageBox(owner, message, title, icon)));
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
