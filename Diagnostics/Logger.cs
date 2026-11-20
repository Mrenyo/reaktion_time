using System.Text;
using System.Threading.Channels;

namespace ReactionTimeMeasurementApp.Diagnostics;

internal enum LogLevel
{
    Info,
    Warning,
    Error
}

// A naplózás minden rétegen átívelő feladat, ezért ez az egyetlen szándékosan statikus szolgáltatás:
// a helyét a composition root (Program) adja meg induláskor, az Initialize hívásával. 
//
// A hívó szálat a fájlírás nem lassítja (a napló a soros olvasóciklusból és a UI-szál kattintáskezelőjéből
// is hívódik): a sorok egy sorba (Channel) kerülnek, és egyetlen háttérszál írja ki őket kötegekben. Hiba
// szintű sornál a hívó legfeljebb ErrorWaitTimeout ideig megvárja a kiírást, hogy egy rögtön utána
// következő összeomlás se vigye el. A fájl megosztva (FileShare.ReadWrite) nyílik meg, így egy második
// programpéldány is írhat bele; ha az írás mégsem sikerül, a sorok a memóriában várnak, és a háttérszál
// újrapróbálkozik. A Shutdown után (pl. a Program.Main legvégén felszabaduló szolgáltatások sorai) az
// írás közvetlenül, a hívó szálán történik. A napló soha nem dob kivételt a hívónak.
internal static class Logger
{
    private static readonly TimeSpan ErrorWaitTimeout = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan WriteRetryDelay = TimeSpan.FromMilliseconds(200);

    // Ennyi kiíratlan karakter fölött (tartósan írhatatlan fájlnál) a legrégebbi várakozó sorok elvesznek,
    // hogy a memória ne nőjön korlátlanul; az elveszett sorok számát a következő sikeres írás jelzi.
    private const int MaxPendingChars = 4 * 1024 * 1024;

    private static readonly Channel<QueuedLine> Queue = Channel.CreateUnbounded<QueuedLine>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    // Védi a _logFilePath-t, a _writerThread-et, a közvetlen (Shutdown utáni) írást és a kiírt sorok
    // számlálóját (_writtenSequence), amelyre a Hiba szintű hívó vár.
    private static readonly object StateLock = new();
    private static string? _logFilePath;
    private static Thread? _writerThread;
    private static long _enqueuedSequence;
    private static long _writtenSequence;

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

        lock (StateLock)
        {
            _logFilePath = Path.Combine(logsDirectory, $"log-{DateTime.Now:yyyy-MM-dd}.txt");
            if (_writerThread is null)
            {
                _writerThread = new Thread(RunWriter) { IsBackground = true, Name = "Logger" };
                _writerThread.Start();

                // Ha a program a Program.Main vége előtt lép ki (pl. Environment.Exit), a sorban álló sorok
                // ekkor íródnak ki.
                AppDomain.CurrentDomain.ProcessExit += (_, _) => Shutdown();
            }
        }
    }

    internal static void Info(string message) => Write(LogLevel.Info, message);

    internal static void Warning(string message) => Write(LogLevel.Warning, message);

    internal static void Error(string message, Exception? exception = null)
    {
        var fullMessage = exception is null ? message : $"{message}{Environment.NewLine}{exception}";
        Write(LogLevel.Error, fullMessage);
    }

    // A sorban álló sorok kiírása és a háttérszál leállítása, legfeljebb ShutdownTimeout ideig várva.
    // Többször is hívható; utána a napló közvetlenül ír.
    internal static void Shutdown()
    {
        Thread? writerThread;
        lock (StateLock)
        {
            writerThread = _writerThread;
        }

        Queue.Writer.TryComplete();
        writerThread?.Join(ShutdownTimeout);
    }

    private static void Write(LogLevel level, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
        System.Diagnostics.Debug.WriteLine(line);

        long sequence;
        lock (StateLock)
        {
            if (_logFilePath is null)
            {
                return;
            }

            sequence = ++_enqueuedSequence;
            if (!Queue.Writer.TryWrite(new QueuedLine(line, sequence)))
            {
                // A Shutdown után: közvetlen írás (a sorrend a lock miatt megmarad).
                TryAppend(_logFilePath, line + Environment.NewLine);
                return;
            }

            if (level != LogLevel.Error)
            {
                return;
            }

            var deadline = DateTime.UtcNow + ErrorWaitTimeout;
            while (_writtenSequence < sequence)
            {
                var remaining = deadline - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero || !Monitor.Wait(StateLock, remaining))
                {
                    return;
                }
            }
        }
    }

    // A háttérszál: megvárja a következő sort, kiolvassa az összes várakozót, és egy kötegben a fájl
    // végére írja. Sikertelen írásnál a köteg megmarad, és WriteRetryDelay után újra próbálkozik.
    private static void RunWriter()
    {
        var reader = Queue.Reader;
        var pending = new StringBuilder();
        long pendingSequence = 0;
        var droppedLines = 0;

        while (true)
        {
            if (pending.Length == 0 && !reader.WaitToReadAsync().AsTask().GetAwaiter().GetResult())
            {
                return; // a Shutdown lezárta a sort, és minden sor kiíródott
            }

            while (reader.TryRead(out var queued))
            {
                if (pending.Length + queued.Text.Length > MaxPendingChars)
                {
                    droppedLines += CountLines(pending);
                    pending.Clear();
                }

                pending.Append(queued.Text).Append(Environment.NewLine);
                pendingSequence = queued.Sequence;
            }

            string? path;
            lock (StateLock)
            {
                path = _logFilePath;
            }

            var text = droppedLines == 0
                ? pending.ToString()
                : $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{LogLevel.Warning}] {droppedLines} naplósor elveszett, mert a naplófájl "
                    + $"sokáig nem volt írható.{Environment.NewLine}{pending}";

            if (path is not null && TryAppend(path, text))
            {
                pending.Clear();
                droppedLines = 0;
                lock (StateLock)
                {
                    _writtenSequence = pendingSequence;
                    Monitor.PulseAll(StateLock);
                }
            }
            else
            {
                Thread.Sleep(WriteRetryDelay);
            }
        }
    }

    private static bool TryAppend(string path, string text)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
            stream.Write(Encoding.UTF8.GetBytes(text));
            return true;
        }
        catch (Exception ex)
        {
            // A naplózás best-effort: a hiba (pl. zárolt fájl) nem szakíthatja meg a folyamatban lévő mérést.
            System.Diagnostics.Debug.WriteLine($"A napló nem írható ({path}): {ex.Message}");
            return false;
        }
    }

    private static int CountLines(StringBuilder builder)
    {
        var count = 0;
        foreach (var chunk in builder.GetChunks())
        {
            count += chunk.Span.Count('\n');
        }

        return count;
    }

    private readonly record struct QueuedLine(string Text, long Sequence);
}
