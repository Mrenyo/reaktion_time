using ReactionTimeMeasurementApp.Diagnostics;

namespace ReactionTimeMeasurementApp.Infrastructure;

// Közös fájlműveletek a tartós (áramszünetet is túlélő) íráshoz és az átmeneti zárolások kezeléséhez.
// Az adatmappa a Dokumentumok alatt van, amit gyakran a OneDrive szinkronizál; a OneDrive és a
// víruskeresők rövid ideig zárolhatják a fájlt, ezért az írás és a csere néhányszor újrapróbálkozik.
internal static class DurableFile
{
    private const int AttemptCount = 5;

    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(100);

    // Átmeneti (pl. a OneDrive vagy egy víruskereső rövid zárolása) lehet minden I/O-hiba, kivéve a hiányzó
    // fájlt / mappát és a túl hosszú útvonalat: ezeken az újrapróbálás nem segít. A jogosultsági hiba
    // (UnauthorizedAccessException) általában végleges, ezért csak ott számít átmenetinek, ahol a hívó kéri
    // (retryAccessDenied): a File.Replace egy zárolt fájlnál is ezt dobja.
    private static bool IsTransient(Exception exception, bool retryAccessDenied)
    {
        return (retryAccessDenied && exception is UnauthorizedAccessException)
            || exception is IOException and not FileNotFoundException and not DirectoryNotFoundException and not PathTooLongException;
    }

    // A művelet végrehajtása; átmeneti hibánál RetryDelay várakozás után újra, legfeljebb AttemptCount
    // alkalommal. Az utolsó kísérlet kivétele a hívóhoz jut. A description a napló számára: pl.
    // "A beállításfájl cseréje".
    internal static T Retry<T>(string description, Func<T> operation, bool retryAccessDenied = false)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return operation();
            }
            catch (Exception ex) when (attempt < AttemptCount && IsTransient(ex, retryAccessDenied))
            {
                Logger.Warning($"{description} nem sikerült ({attempt}. kísérlet), újrapróbálás: {ex.Message}");
                Thread.Sleep(RetryDelay);
            }
        }
    }

    internal static void Retry(string description, Action operation, bool retryAccessDenied = false)
    {
        Retry(
            description,
            () =>
            {
                operation();
                return true;
            },
            retryAccessDenied);
    }

    // Új fájl megnyitása írásra; meglévő fájlt sosem ír felül. Null, ha a fájl már létezik. A stream
    // puffer nélküli, így minden Write közvetlenül az operációs rendszerhez kerül.
    internal static FileStream? TryCreateNew(string path, FileShare share)
    {
        try
        {
            return new FileStream(path, FileMode.CreateNew, FileAccess.Write, share, bufferSize: 0);
        }
        catch (IOException) when (File.Exists(path))
        {
            return null;
        }
    }

    // A bájtok kiírása a stream aktuális pozíciójára úgy, hogy a visszatéréskor már a lemezen legyenek
    // (nem csak az operációs rendszer gyorsítótárában). Átmeneti hibánál a kiírás ugyanarról a pozícióról
    // kezdődik újra, így egy félig kiírt rész felülíródik, és nem kerül kétszer a fájlba.
    internal static void WriteDurably(FileStream stream, byte[] bytes)
    {
        var start = stream.Position;
        Retry($"A fájl írása ({stream.Name})", () =>
        {
            stream.Position = start;
            stream.Write(bytes);
            stream.Flush(flushToDisk: true);
        });
    }

    // Atomikus csere: a tartalom előbb a <fájl>.tmp fájlba kerül (lemezre ürítve), és csak utána lép a
    // célfájl helyére, így egy írás közbeni hiba (áramszünet, tele lemez) sosem hagy félig írt célfájlt.
    // A felülírt változat a backupPath néven marad meg. A cserét átmeneti hibánál újrapróbálja; az
    // ideiglenes fájl hiba esetén sem marad meg.
    internal static void ReplaceAtomically(string path, byte[] content, string backupPath, string description)
    {
        var tempPath = path + ".tmp";
        try
        {
            using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 0))
            {
                stream.Write(content);
                stream.Flush(flushToDisk: true);
            }

            Retry(description, () =>
            {
                if (File.Exists(path))
                {
                    File.Replace(tempPath, path, backupPath, ignoreMetadataErrors: true);
                }
                else
                {
                    File.Move(tempPath, path);
                }
            }, retryAccessDenied: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                TryDelete(tempPath);
            }
        }
    }

    internal static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            Logger.Warning($"A fájl nem törölhető ({path}): {ex.Message}");
        }
    }
}
