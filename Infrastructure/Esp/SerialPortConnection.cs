using System.IO.Ports;
using System.Text;
using ReactionTimeMeasurementApp.Diagnostics;

namespace ReactionTimeMeasurementApp.Infrastructure.Esp;

// A System.IO.Ports.SerialPort soralapú burkolója: 8 adatbit, nincs paritás, 1 stopbit, nincs
// kézfogás, UTF-8, a sorok vége "\n". Az írás és a lezárás szálbiztos; olvasni egyszerre egy hívó olvashat.
internal sealed class SerialPortConnection : IDisposable
{
    private const int WriteAttemptCount = 3;
    private const int ReadTimeoutMilliseconds = 200;

    // Ennyi egymást követő olvasási hiba után a portot használhatatlannak tekintjük (pl. kihúzott eszköz).
    private const int MaxConsecutiveReadErrors = 10;
    private static readonly TimeSpan WriteRetryDelay = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan WriteTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ReadErrorDelay = TimeSpan.FromMilliseconds(100);

    private readonly SerialPort _port;
    private readonly object _portLock = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public SerialPortConnection(string portName, int baudRate)
    {
        _port = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
        {
            NewLine = "\n",
            ReadTimeout = ReadTimeoutMilliseconds,
            WriteTimeout = SerialPort.InfiniteTimeout,
            Encoding = Encoding.UTF8,
            Handshake = Handshake.None
        };
    }

    public void Open()
    {
        lock (_portLock)
        {
            if (!_port.IsOpen)
            {
                _port.Open();
            }
        }

        ClearBuffers();
    }

    // Nem dob kivételt: egy kihúzott eszköz portjának lezárása IOException-nel vagy
    // UnauthorizedAccessException-nel is meghiúsulhat, de a lezárást ilyenkor sem lehet megismételni.
    public void Close()
    {
        try
        {
            lock (_portLock)
            {
                if (_port.IsOpen)
                {
                    _port.Close();
                }
            }
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            Logger.Warning($"[SERIAL] A port lezárása közben hiba történt: {ex.Message}");
        }
    }

    public void ClearBuffers()
    {
        TryOnOpenPort(port => port.DiscardInBuffer());
        TryOnOpenPort(port => port.DiscardOutBuffer());
    }

    // Átmeneti írási hibánál néhányszor újrapróbálja. Ha a port az időkorláton belül nem veszi át az
    // adatot, a kapcsolatot lezárja, és IOException-t dob (ilyenkor nincs újrapróbálás).
    public async Task WriteLineAsync(string payload, CancellationToken cancellationToken = default)
    {
        var bytes = Encoding.UTF8.GetBytes(payload + _port.NewLine);

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            for (var attempt = 1; ; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    await WriteRawAsync(bytes, cancellationToken).ConfigureAwait(false);
                    return;
                }
                catch (IOException ex) when (ex is not SerialWriteTimedOutException && attempt < WriteAttemptCount)
                {
                    TryOnOpenPort(port => port.DiscardOutBuffer());
                    await Task.Delay(WriteRetryDelay, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    // A következő beérkező sor (a sorvég nélkül); null, ha a port lezárult, használhatatlanná vált,
    // vagy a hívó megszakította a várakozást.
    //
    // A blokkoló ReadLine (legfeljebb ReadTimeout ideig) NEM fut a _portLock alatt: különben az írás
    // és a lezárás minden olvasási ciklusban várakozna. A SerialPort egyidejű olvasást és írást
    // megenged; ha olvasás közben zárják le a portot, a ReadLine kivételt dob, amit az alábbi ágak kezelnek.
    public Task<string?> ReadLineAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run<string?>(() =>
        {
            var consecutiveErrors = 0;
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    lock (_portLock)
                    {
                        if (!_port.IsOpen)
                        {
                            return null;
                        }
                    }

                    return _port.ReadLine().TrimEnd('\r');
                }
                catch (TimeoutException)
                {
                    // Nem jött teljes sor az időkorláton belül: tovább várunk.
                    consecutiveErrors = 0;
                }
                catch (Exception ex) when (ex is ObjectDisposedException || ex is InvalidOperationException)
                {
                    return null;
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    // Kihúzott eszköznél jellemzően UnauthorizedAccessException; az átmeneti hibákat
                    // néhányszor kivárjuk, utána a portot használhatatlannak tekintjük.
                    if (++consecutiveErrors >= MaxConsecutiveReadErrors)
                    {
                        return null;
                    }

                    Thread.Sleep(ReadErrorDelay);
                }
            }

            return null;
        }, CancellationToken.None);
    }

    public void Dispose()
    {
        Close();
        _writeLock.Dispose();

        try
        {
            _port.Dispose();
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            Logger.Warning($"[SERIAL] A port felszabadítása közben hiba történt: {ex.Message}");
        }
    }

    private async Task WriteRawAsync(byte[] bytes, CancellationToken cancellationToken)
    {
        Stream stream;

        lock (_portLock)
        {
            if (!_port.IsOpen)
            {
                throw new IOException("A soros port nincs megnyitva.");
            }

            stream = _port.BaseStream;
        }

        var writeTask = WriteAndFlushAsync(stream, bytes, cancellationToken);
        var timeoutTask = Task.Delay(WriteTimeout, cancellationToken);
        if (await Task.WhenAny(writeTask, timeoutTask).ConfigureAwait(false) == writeTask)
        {
            await writeTask.ConfigureAwait(false);
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        Close();

        // A félbemaradt írás későbbi hibája ne maradjon megfigyeletlen.
        _ = writeTask.ContinueWith(
            completedTask => _ = completedTask.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        throw new SerialWriteTimedOutException(
            $"A soros port {WriteTimeout.TotalMilliseconds:0} ms alatt nem vette át az írandó adatot, ezért a kapcsolat le lett zárva.");
    }

    private static async Task WriteAndFlushAsync(Stream stream, byte[] bytes, CancellationToken cancellationToken)
    {
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    // A pufferek ürítése best-effort: lezárt vagy hibás porton nincs mit üríteni.
    private void TryOnOpenPort(Action<SerialPort> action)
    {
        try
        {
            lock (_portLock)
            {
                if (_port.IsOpen)
                {
                    action(_port);
                }
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException || ex is IOException)
        {
        }
    }

    private sealed class SerialWriteTimedOutException : IOException
    {
        public SerialWriteTimedOutException(string message)
            : base(message)
        {
        }
    }
}
