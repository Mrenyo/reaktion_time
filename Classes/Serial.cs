using System.IO.Ports;

namespace ReactionTimeMeasurementApp.Classes
{
    internal sealed class Serial : IAsyncDisposable
    {
        private readonly ISerialConnection _connection;
        private readonly List<PendingRequest> _pendingRequests = new();
        private readonly object _pendingLock = new();

        private CancellationTokenSource? _readLoopCts;
        private Task? _readLoopTask;
        private bool _disposed;

        public event Action<string>? MessageReceived;

        public bool IsRunning => _readLoopTask is { IsCompleted: false };

        public static string[] GetAvailablePorts() => SystemIoSerialConnection.GetPortNames();

        public Serial(string portName, int baudRate = 9600, Parity parity = Parity.None, int dataBits = 8, StopBits stopBits = StopBits.One)
            : this(new SystemIoSerialConnection(portName, baudRate, parity, dataBits, stopBits))
        {
        }

        internal Serial(ISerialConnection connection)
        {
            _connection = connection;
        }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (IsRunning)
            {
                return Task.CompletedTask;
            }

            _connection.Open();
            _readLoopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _readLoopTask = Task.Run(() => ReadLoopAsync(_readLoopCts.Token), CancellationToken.None);

            return Task.CompletedTask;
        }

        public async Task StopAsync()
        {
            if (_readLoopCts is null)
            {
                return;
            }

            _readLoopCts.Cancel();

            if (_readLoopTask is not null)
            {
                await _readLoopTask.ConfigureAwait(false);
            }

            _readLoopTask = null;
            _readLoopCts.Dispose();
            _readLoopCts = null;

            lock (_pendingLock)
            {
                foreach (var request in _pendingRequests)
                {
                    request.TrySetCanceled();
                }

                _pendingRequests.Clear();
            }

            _connection.Close();
        }

        public Task SendAsync(string payload, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (!IsRunning)
            {
                throw new InvalidOperationException("A soros kapcsolat nincs elindítva. Hívd meg a StartAsync metódust.");
            }

            return _connection.WriteLineAsync(payload, cancellationToken);
        }

        public async Task<string> RequestResponseAsync(
            string request,
            Func<string, bool>? responseMatcher = null,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (!IsRunning)
            {
                throw new InvalidOperationException("A kérés-válasz funkció használata előtt hívd meg a StartAsync metódust.");
            }

            var effectiveMatcher = responseMatcher ?? (_ => true);
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var pendingRequest = new PendingRequest(effectiveMatcher, tcs);

            lock (_pendingLock)
            {
                _pendingRequests.Add(pendingRequest);
            }

            try
            {
                await SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                lock (_pendingLock)
                {
                    _pendingRequests.Remove(pendingRequest);
                }

                throw;
            }

            return await AwaitPendingRequestAsync(pendingRequest, timeout, cancellationToken).ConfigureAwait(false);
        }

        public async Task<string> WaitForResponseAsync(
            Func<string, bool>? responseMatcher = null,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (!IsRunning)
            {
                throw new InvalidOperationException("Válaszra várakozás előtt hívd meg a StartAsync metódust.");
            }

            var effectiveMatcher = responseMatcher ?? (_ => true);
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var pendingRequest = new PendingRequest(effectiveMatcher, tcs);

            lock (_pendingLock)
            {
                _pendingRequests.Add(pendingRequest);
            }

            return await AwaitPendingRequestAsync(pendingRequest, timeout, cancellationToken).ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            await StopAsync().ConfigureAwait(false);
            _connection.Dispose();
            _disposed = true;
        }

        private async Task ReadLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                string line;

                try
                {
                    line = await _connection.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                MessageReceived?.Invoke(line);
                TryCompletePendingRequest(line);
            }
        }

        private void TryCompletePendingRequest(string line)
        {
            lock (_pendingLock)
            {
                var match = _pendingRequests.FirstOrDefault(request => request.Matcher(line));
                match?.TrySetResult(line);
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(Serial));
            }
        }

        private async Task<string> AwaitPendingRequestAsync(
            PendingRequest pendingRequest,
            TimeSpan? timeout,
            CancellationToken cancellationToken)
        {
            try
            {
                if (!timeout.HasValue)
                {
                    using var registration = cancellationToken.Register(() => pendingRequest.TrySetCanceled());
                    return await pendingRequest.Task.ConfigureAwait(false);
                }

                var timeoutTask = Task.Delay(timeout.Value, CancellationToken.None);
                var cancellationTask = Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                var completedTask = await Task.WhenAny(pendingRequest.Task, timeoutTask, cancellationTask).ConfigureAwait(false);

                if (completedTask == pendingRequest.Task)
                {
                    return await pendingRequest.Task.ConfigureAwait(false);
                }

                if (completedTask == cancellationTask)
                {
                    pendingRequest.TrySetCanceled();
                    cancellationToken.ThrowIfCancellationRequested();
                }

                return string.Empty;
            }
            finally
            {
                lock (_pendingLock)
                {
                    _pendingRequests.Remove(pendingRequest);
                }
            }
        }

        private sealed class PendingRequest
        {
            private readonly Func<string, bool> _matcher;
            private readonly TaskCompletionSource<string> _completionSource;

            public PendingRequest(Func<string, bool> matcher, TaskCompletionSource<string> completionSource)
            {
                _matcher = matcher;
                _completionSource = completionSource;
            }

            public Task<string> Task => _completionSource.Task;

            public Func<string, bool> Matcher => _matcher;

            public bool TrySetResult(string value) => _completionSource.TrySetResult(value);

            public bool TrySetCanceled() => _completionSource.TrySetCanceled();

            public bool TrySetException(Exception exception) => _completionSource.TrySetException(exception);
        }
    }

    internal interface ISerialConnection : IDisposable
    {
        void Open();

        void Close();

        Task WriteLineAsync(string payload, CancellationToken cancellationToken = default);

        Task<string> ReadLineAsync(CancellationToken cancellationToken = default);
    }

    internal sealed class SystemIoSerialConnection : ISerialConnection
    {
        private readonly SerialPort _port;
        private readonly System.Text.StringBuilder _buffer = new();

        public SystemIoSerialConnection(string portName, int baudRate, Parity parity, int dataBits, StopBits stopBits)
        {
            _port = new SerialPort(portName, baudRate, parity, dataBits, stopBits)
            {
                NewLine = "\n",
                ReadTimeout = SerialPort.InfiniteTimeout,
                WriteTimeout = 1000,
                Encoding = System.Text.Encoding.UTF8
            };
        }

        public static string[] GetPortNames() => SerialPort.GetPortNames();

        public void Open()
        {
            if (!_port.IsOpen)
            {
                _port.Open();
            }
        }

        public void Close()
        {
            if (_port.IsOpen)
            {
                _port.Close();
            }
        }

        public Task WriteLineAsync(string payload, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.CompletedTask;
            }

            return Task.Run(() =>
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    _port.WriteLine(payload);
                }
            }, CancellationToken.None);
        }

        public Task<string> ReadLineAsync(CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                while (true)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return string.Empty;
                    }

                    try
                    {
                        var incoming = _port.ReadExisting();
                        if (!string.IsNullOrEmpty(incoming))
                        {
                            _buffer.Append(incoming);
                            var bufferText = _buffer.ToString();
                            var newlineIndex = bufferText.IndexOf('\n');
                            if (newlineIndex >= 0)
                            {
                                var line = bufferText[..newlineIndex].TrimEnd('\r');
                                _buffer.Remove(0, newlineIndex + 1);
                                return line;
                            }
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        return string.Empty;
                    }
                    catch (InvalidOperationException)
                    {
                        return string.Empty;
                    }

                    Thread.Sleep(5);
                }
            }, CancellationToken.None);
        }

        public void Dispose()
        {
            _port.Dispose();
        }
    }
}
