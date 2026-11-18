using ReactionTimeMeasurementApp.Diagnostics;

namespace ReactionTimeMeasurementApp.UI;

// A felhasználónak szóló üzenetablakok (hibák, figyelmeztetések és tájékoztatások) egy helyen; minden
// megjelenített üzenet a naplóba is bekerül.
internal static class ErrorPopup
{
    // A Win32 ERROR_ACCESS_DENIED és ERROR_SHARING_VIOLATION hibakódja HRESULT alakban.
    private const int AccessDeniedHResult = unchecked((int)0x80070005);
    private const int SharingViolationHResult = unchecked((int)0x80070020);

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
            "A tekintet-ellenőrzéses méréshez először válassz ki vagy végezz el egy kalibrációt. "
            + "(A tekintet-ellenőrzés a Testreszabásban kikapcsolható.)",
            "Kalibráció szükséges",
            MessageBoxIcon.Warning);
    }

    internal static void ShowSettingsError(Control? owner, string settingsPath, string details)
    {
        Show(
            owner,
            "A mérési beállítások hibásak, ezért a mérés nem indul el. Javítsd őket a főmenü Testreszabás gombjával, majd próbáld újra."
            + $"{Environment.NewLine}{Environment.NewLine}Fájl: {settingsPath}{Environment.NewLine}{Environment.NewLine}{details}",
            "Hibás mérési beállítások",
            MessageBoxIcon.Error);
    }

    internal static void ShowSettingsSaveFailed(Control? owner, string path, Exception exception)
    {
        Show(
            owner,
            AppendDetails(
                "A beállítások mentése nem sikerült; a módosítások az ablakban megmaradtak. Ellenőrizd, hogy a fájl írható-e "
                + "(nem írásvédett, és más program sem tartja nyitva), majd próbáld újra."
                + $"{Environment.NewLine}{Environment.NewLine}Fájl: {path}",
                exception),
            "A beállítások nem menthetők",
            MessageBoxIcon.Error,
            exception);
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

    // A Tobii Pro Eye Tracker Manager kalibrációja nem sikerült, vagy a felhasználó megszakította. A
    // program kimenete (a kivétel szövegében) csak a naplóba kerül, az ablakba nem.
    internal static void ShowCalibrationFailed(Control? owner, Exception exception)
    {
        Show(
            owner,
            "A kalibráció nem fejeződött be (megszakadt, vagy a Tobii Pro Eye Tracker Manager hibát jelzett), ezért a korábbi "
            + "állapot maradt érvényben. Próbáld újra; ha a hiba megmarad, a részletek a naplófájlban találhatók.",
            "A kalibráció nem sikerült",
            MessageBoxIcon.Warning,
            exception);
    }

    internal static void ShowEyeTrackerManagerMissing(Control? owner, Exception exception)
    {
        Show(
            owner,
            "A kalibrációhoz szükséges Tobii Pro Eye Tracker Manager nem található ezen a gépen. Telepítsd, majd próbáld újra.",
            "Hiányzó kalibrációs program",
            MessageBoxIcon.Error,
            exception);
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
        var portBusy = IsPortBusyError(exception);
        var message = portBusy
            ? $"A(z) {portName} port foglalt, ezért az alkalmazás nem tudja megnyitni. Zárd be a Serial Monitort, "
                + "a PlatformIO/Arduino monitort vagy a másik futó alkalmazáspéldányt, majd próbáld újra."
            : $"Az ESP nem érhető el a(z) {portName} porton. Ellenőrizd a csatlakozást, majd próbáld újra.";
        Show(owner, AppendDetails(message, exception), portBusy ? "COM-port foglalt" : "ESP kapcsolati hiba", MessageBoxIcon.Error, exception);
    }

    internal static void ShowEspCommunicationError(Control? owner, string portName, int baudRate, Exception? exception = null)
    {
        var message = $"Az ESP a(z) {portName} porton megnyílt, de nem válaszolt a kapcsolatellenőrzésre (a „CHECK” üzenetre „OK”-val). "
            + $"Ellenőrizd, hogy az ESP-n a méréshez tartozó firmware fut-e, és {baudRate} baudon kommunikál-e, majd próbáld újra.";
        Show(owner, AppendDetails(message, exception), "ESP kommunikációs hiba", MessageBoxIcon.Error, exception);
    }

    // A mérés végén jelenik meg: mérés közben egy felugró ablak a résztvevő elé kerülne.
    internal static void ShowEspConnectionLost(Control? owner, string portName, string trialLabel, Exception exception)
    {
        var message = $"Az ESP-vel ({portName}) a mérés közben, a(z) {trialLabel}. próbában megszakadt a kommunikáció. A mérés innentől "
            + "ESP nélkül, a számítógép által érzékelt kattintásokkal folytatódott, így ezekben a próbákban csak a helyi (PC-n mért) "
            + "reakcióidő van meg. A következő mérés előtt ellenőrizd az ESP csatlakozását.";
        Show(owner, AppendDetails(message, exception), "Megszakadt az ESP-kapcsolat", MessageBoxIcon.Warning, exception);
    }

    internal static void ShowTrackerConnectionLost(Control? owner, string trialLabel, bool measurementAborted)
    {
        var message = measurementAborted
            ? $"A szemkövető kapcsolata a mérés közben, a(z) {trialLabel}. próbában megszakadt. A tekintet-ellenőrzéshez szemkövető "
                + "kell, ezért a mérés leállt; az addigi próbák mentve vannak. Ellenőrizd a szemkövető csatlakozását, majd indítsd újra a mérést."
            : $"A szemkövető kapcsolata a mérés közben, a(z) {trialLabel}. próbában megszakadt. A mérés tekintetadat nélkül folytatódott. "
                + "A következő mérés előtt ellenőrizd a szemkövető csatlakozását.";
        Show(owner, message, "Megszakadt a szemkövető kapcsolata", MessageBoxIcon.Warning);
    }

    internal static void ShowGeneral(Control? owner, Exception? exception = null)
    {
        const string message = "Váratlan hiba történt. Próbáld újra, vagy indítsd újra az alkalmazást.";
        Show(owner, AppendDetails(message, exception), GeneralTitle, MessageBoxIcon.Error, exception);
    }

    // A foglalt portot a kivétel típusa, illetve a Windows hibakódja jelzi (hozzáférés megtagadva vagy
    // megosztási hiba) - a kivétel szövege a rendszer nyelvétől függ, ezért arra nem támaszkodunk.
    private static bool IsPortBusyError(Exception? exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is UnauthorizedAccessException
                || (current is IOException && current.HResult is AccessDeniedHResult or SharingViolationHResult))
            {
                return true;
            }
        }

        return false;
    }

    private static string AppendDetails(string message, Exception? exception)
    {
        return exception is null || string.IsNullOrWhiteSpace(exception.Message)
            ? message
            : $"{message}{Environment.NewLine}{Environment.NewLine}Részletek: {exception.Message}";
    }

    private static void Show(Control? owner, string message, string title, MessageBoxIcon icon, Exception? exception = null)
    {
        // Minden megjelenített üzenet a naplóba is bekerül (a kivétel a stack trace-szel együtt), így egy
        // résztvevőnél tapasztalt hiba utólag is visszakereshető. A UI-szálra váltást a ShowMessageBox
        // végzi, hogy az átirányítás miatt ne naplózzunk kétszer ugyanarról az üzenetről.
        switch (icon)
        {
            case MessageBoxIcon.Error:
                Logger.Error($"[{title}] {message}", exception);
                break;
            case MessageBoxIcon.Information:
                Logger.Info($"[{title}] {message}");
                break;
            default:
                Logger.Warning(exception is null ? $"[{title}] {message}" : $"[{title}] {message}{Environment.NewLine}{exception}");
                break;
        }

        ShowMessageBox(owner, message, title, icon);
    }

    // Háttérszálról hívva az ablak a tulajdonos UI-szálán jelenik meg (a hívás nem várja meg a
    // bezárását). Ha a tulajdonos már megszűnt, az ablak tulajdonos nélkül jelenik meg.
    private static void ShowMessageBox(Control? owner, string message, string title, MessageBoxIcon icon)
    {
        if (owner is { IsDisposed: true })
        {
            owner = null;
        }

        if (owner is not null && owner.InvokeRequired)
        {
            try
            {
                var target = owner;
                target.BeginInvoke(new Action(() => ShowMessageBox(target, message, title, icon)));
                return;
            }
            catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
            {
                // A tulajdonos az ellenőrzés és a hívás között szűnt meg.
                owner = null;
            }
        }

        MessageBox.Show(owner?.FindForm() ?? owner, message, title, MessageBoxButtons.OK, icon);
    }
}
