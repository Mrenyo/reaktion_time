using ReactionTimeMeasurementApp.Diagnostics;

namespace ReactionTimeMeasurementApp.Infrastructure.Esp;

// Soralapú kérés-válasz kommunikáció egy soros porton. A beérkező sorokat egy háttérben futó
// olvasóciklus dolgozza fel: minden sort a MessageReceived eseményen ad tovább, és a legrégebbi
// olyan várakozó kérésnek adja át, amelynek a feltétele (matcher) illik rá. A kérések feltételei
// egymást kizáró formátumúak legyenek, különben egy késve érkező válasz egy későbbi kérést zárhat le.
//
// Ha a port menet közben használhatatlanná válik (pl. kihúzták az eszközt), az olvasóciklus leáll,
// az IsRunning hamis lesz, és a következő küldés InvalidOperationException-t dob.
internal sealed class Serial : IAsyncDisposable
{
    private const int OpenAttemptCount = 8;
    private static readonly TimeSpan OpenRetryDelay = TimeSpan.FromMilliseconds(500);

    // Az ESP a port megnyitásakor újraindulhat; ennyi idő után már fogadja az üzeneteket.
    private static readonly TimeSpan PortStartupDelay = TimeSpan.FromMilliseconds(1500);

    private readonly SerialPortConnection _connection;
    private readonly List<PendingRequest> _pendingRequests = new();
    private readonly object _pendingLock = new();

    private CancellationTokenSource? _readLoopCts;
    private Task? _readLoopTask;
    private bool _disposed;

    public Serial(string portName, int baudRate)
    {
        _connection = new SerialPortConnection(portName, baudRate);
    }

    public event Action<string>? MessageReceived;

    public bool IsRunning => _readLoopTask is { IsCompleted: false };

    // A port megnyitása háttérszálon fut (egyes portoknál, pl. Bluetooth, másodpercekig is tarthat),
    // így a hívó szálat - a mérés indításakor a UI-szálat - nem blokkolja. A cancellationToken csak az
    // indítást szakítja meg, az elindult olvasóciklust a StopAsync állítja le.
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (IsRunning)
        {
            return;
        }

        for (var attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await Task.Run(_connection.Open, CancellationToken.None).ConfigureAwait(false);
                break;
            }
            catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException) && attempt < OpenAttemptCount)
            {
                // A portot rövid ideig más is foghatja (pl. az előző kapcsolat lezárása még tart).
                await Task.Delay(OpenRetryDelay, cancellationToken).ConfigureAwait(false);
            }
        }

        await Task.Delay(PortStartupDelay, cancellationToken).ConfigureAwait(false);
        _connection.ClearBuffers();

        _readLoopCts = new CancellationTokenSource();
        var readLoopToken = _readLoopCts.Token;
        _readLoopTask = Task.Run(() => ReadLoopAsync(readLoopToken), CancellationToken.None);
    }

    // Leállítja az olvasóciklust, és a portot mindenképp lezárja: akkor is, ha a ciklus hibával állt
    // le (pl. kihúzott eszköz), vagy el sem indult (az indítás a port megnyitása után szakadt meg).
    private async Task StopAsync()
    {
        var readLoopCts = _readLoopCts;
        var readLoopTask = _readLoopTask;
        _readLoopCts = null;
        _readLoopTask = null;

        try
        {
            readLoopCts?.Cancel();
            if (readLoopTask is not null)
            {
                await readLoopTask.ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Logger.Warning($"[SERIAL] Az olvasóciklus hibával állt le: {ex.Message}");
        }
        finally
        {
            readLoopCts?.Dispose();

            lock (_pendingLock)
            {
                foreach (var request in _pendingRequests)
                {
                    request.Completion.TrySetCanceled();
                }

                _pendingRequests.Clear();
            }

            _connection.Close();
        }
    }

    public Task SendAsync(string payload, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsRunning)
        {
            throw new InvalidOperationException("A soros kapcsolat nem él: nincs elindítva, vagy a port közben használhatatlanná vált.");
        }

        return _connection.WriteLineAsync(payload, cancellationToken);
    }

    // Elküldi a kérést, és megvárja az első olyan beérkező sort, amelyre a responseMatcher igazat ad.
    // Ha az időkorláton belül nem jön ilyen, üres szöveget ad vissza.
    public async Task<string> RequestResponseAsync(
        string request,
        Func<string, bool> responseMatcher,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var pendingRequest = new PendingRequest(
            responseMatcher,
            new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously));

        lock (_pendingLock)
        {
            _pendingRequests.Add(pendingRequest);
        }

        try
        {
            await SendAsync(request, cancellationToken).ConfigureAwait(false);

            var timeoutTask = Task.Delay(timeout, cancellationToken);
            if (await Task.WhenAny(pendingRequest.Completion.Task, timeoutTask).ConfigureAwait(false) != pendingRequest.Completion.Task)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return string.Empty;
            }

            return await pendingRequest.Completion.Task.ConfigureAwait(false);
        }
        finally
        {
            lock (_pendingLock)
            {
                _pendingRequests.Remove(pendingRequest);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            await StopAsync().ConfigureAwait(false);
        }
        finally
        {
            _connection.Dispose();
        }
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            // Null: a port lezárult vagy használhatatlan - a ciklus véget ér (nem pörög tovább).
            var line = await _connection.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    Logger.Warning("[SERIAL] A soros port használhatatlanná vált, az olvasás leállt.");
                }

                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            MessageReceived?.Invoke(line);

            lock (_pendingLock)
            {
                _pendingRequests.FirstOrDefault(request => request.Matcher(line))?.Completion.TrySetResult(line);
            }
        }
    }

    private sealed record PendingRequest(Func<string, bool> Matcher, TaskCompletionSource<string> Completion);
}
