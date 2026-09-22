namespace ReactionTimeMeasurementApp.Diagnostics;

internal enum LogLevel
{
    Info,
    Warning,
    Error
}

// Egyszerű, fájlba is író napló. A Debug.WriteLine Release buildben semmit nem ír ki, ezért minden
// üzenet egy "logs" mappába is kerül (a mérési adatok mellé): így egy résztvevőnél történt hibás
// mérés utólag is visszakövethető.
//
// A naplózás minden rétegen átívelő feladat, ezért ez az egyetlen szándékosan statikus szolgáltatás:
// a helyét a composition root (Program) adja meg induláskor, az Initialize hívásával. Előtte csak a
// Debug kimenetre ír.
internal static class Logger
{
    private static readonly object WriteLock = new();
    private static string? _logFilePath;

    internal static void Initialize(string logsDirectory)
    {
        try
        {
            Directory.CreateDirectory(logsDirectory);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine($"A naplómappa nem hozható létre ({logsDirectory}): {ex.Message}");
        }

        lock (WriteLock)
        {
            _logFilePath = Path.Combine(logsDirectory, $"log-{DateTime.Now:yyyy-MM-dd}.txt");
        }
    }

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
            if (_logFilePath is null)
            {
                return;
            }

            try
            {
                File.AppendAllText(_logFilePath, line + Environment.NewLine);
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
}
