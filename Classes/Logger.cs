namespace ReactionTimeMeasurementApp.Classes;

internal enum LogLevel
{
    Info,
    Warning,
    Error
}

// Egyszerű, fájlba is író napló. A korábbi Functions.Debug (System.Diagnostics.Debug.WriteLine)
// Release build alatt semmit nem ír ki, ezért egy résztvevőnél történt hibás mérés utólag
// visszakövethetetlen volt. Ez az osztály ugyanazt az üzenetet mindig egy "logs" mappába is kiírja
// (a mérési adatok mellé, lásd DataLocations), így Release módban is marad nyoma a futásnak.
internal static class Logger
{
    private static readonly object WriteLock = new();
    private static readonly string LogFilePath = BuildLogFilePath();

    internal static void Info(string message) => Write(LogLevel.Info, message);

    internal static void Warning(string message) => Write(LogLevel.Warning, message);

    internal static void Error(string message, Exception? exception = null)
    {
        var fullMessage = exception is null ? message : $"{message}{Environment.NewLine}{exception}";
        Write(LogLevel.Error, fullMessage);
    }

    private static void Write(LogLevel level, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";

        System.Diagnostics.Debug.WriteLine(line);

        lock (WriteLock)
        {
            try
            {
                File.AppendAllText(LogFilePath, line + Environment.NewLine);
            }
            catch (IOException)
            {
                // A naplózás best-effort: ha a fájl épp zárolva van (pl. párhuzamosan megnyitva),
                // ez nem szakíthatja meg a folyamatban lévő mérést.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private static string BuildLogFilePath()
    {
        var logDirectory = DataLocations.LogsDirectory;

        try
        {
            Directory.CreateDirectory(logDirectory);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return Path.Combine(logDirectory, $"log-{DateTime.Now:yyyy-MM-dd}.txt");
    }
}
