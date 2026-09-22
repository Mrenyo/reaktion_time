using System.IO.Ports;

namespace ReactionTimeMeasurementApp.Infrastructure.Esp
{
    internal sealed class Serial : IAsyncDisposable
    {
        private readonly ISerialConnection _connection;
        private const int OpenAttemptCount = 8;
        private static readonly TimeSpan OpenRetryDelay = TimeSpan.FromMilliseconds(500);
        private static readonly TimeSpan PortStartupDelay = TimeSpan.FromMilliseconds(1500);
        private readonly List<PendingRequest> _pendingRequests = new();
        private readonly object _pendingLock = new();

        private CancellationTokenSource? _readLoopCts;
        private Task? _readLoopTask;
        private bool _disposed;

        public event Action<string>? MessageReceived;

        public bool IsRunning => _readLoopTask is { IsCompleted: false };

        public static string GetPreferredEspPortName() => SystemIoSerialConnection.GetPreferredEspPortName();

        public Serial(string portName, int baudRate = 9600, Parity parity = Parity.None, int dataBits = 8, StopBits stopBits = StopBits.One)
            : this(new SystemIoSerialConnection(portName, baudRate, parity, dataBits, stopBits))
        {
        }

        internal Serial(ISerialConnection connection)
        {
            _connection = connection;
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (IsRunning)
            {
                return;
            }

            for (var attempt = 1; ; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    _connection.Open();
                    break;
                }
                catch (IOException) when (attempt < OpenAttemptCount)
                {
                    await Task.Delay(OpenRetryDelay, cancellationToken).ConfigureAwait(false);
                }
                catch (UnauthorizedAccessException) when (attempt < OpenAttemptCount)
                {
                    await Task.Delay(OpenRetryDelay, cancellationToken).ConfigureAwait(false);
                }
            }

            await Task.Delay(PortStartupDelay, cancellationToken).ConfigureAwait(false);
            _connection.ClearBuffers();

            _readLoopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _readLoopTask = Task.Run(() => ReadLoopAsync(_readLoopCts.Token), CancellationToken.None);
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
        }
    }

    internal interface ISerialConnection : IDisposable
    {
        void Open();

        void Close();

        void ClearBuffers();

        Task WriteLineAsync(string payload, CancellationToken cancellationToken = default);

        Task<string> ReadLineAsync(CancellationToken cancellationToken = default);
    }

    internal sealed class SystemIoSerialConnection : ISerialConnection
    {
        private readonly SerialPort _port;
        private readonly object _portLock = new();
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private const int IoAttemptCount = 3;
        private static readonly TimeSpan IoRetryDelay = TimeSpan.FromMilliseconds(300);
        private const int WriteTimeoutMilliseconds = 5000;
        private const int EspCandidateScore = 50;

        public SystemIoSerialConnection(string portName, int baudRate, Parity parity, int dataBits, StopBits stopBits)
        {
            _port = new SerialPort(portName, baudRate, parity, dataBits, stopBits)
            {
                NewLine = "\n",
                ReadTimeout = 200,
                WriteTimeout = SerialPort.InfiniteTimeout,
                Encoding = System.Text.Encoding.UTF8,
                Handshake = Handshake.None
            };
        }

        public static string GetPreferredEspPortName()
        {
            var candidates = GetPortCandidates();
            if (candidates.Length == 0)
            {
                throw new IOException("Nem található elérhető COM-port.");
            }

            var preferred = candidates.FirstOrDefault(candidate => candidate.Score >= EspCandidateScore);
            if (preferred is not null)
            {
                return preferred.PortName;
            }

            if (candidates.Length == 1)
            {
                return candidates[0].PortName;
            }

            var availablePorts = string.Join(", ", candidates.Select(candidate => candidate.DisplayText));
            throw new IOException(
                $"Nem sikerült egyértelműen azonosítani az ESP COM-portját. Elérhető portok: {availablePorts}");
        }

        private static SerialPortCandidate[] GetPortCandidates()
        {
            var portNames = SerialPort.GetPortNames()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(GetPortNumber)
                .ThenBy(portName => portName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var availablePorts = new HashSet<string>(portNames, StringComparer.OrdinalIgnoreCase);
            var candidatesByPort = portNames.ToDictionary(
                portName => portName,
                portName => new SerialPortCandidate(portName, string.Empty, string.Empty, 0),
                StringComparer.OrdinalIgnoreCase);

            foreach (var candidate in ReadRegistryPortCandidates(availablePorts))
            {
                if (!candidatesByPort.TryGetValue(candidate.PortName, out var existingCandidate)
                    || candidate.Score > existingCandidate.Score)
                {
                    candidatesByPort[candidate.PortName] = candidate;
                }
            }

            return candidatesByPort.Values
                .OrderByDescending(candidate => candidate.Score)
                .ThenBy(candidate => GetPortNumber(candidate.PortName))
                .ThenBy(candidate => candidate.PortName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static IEnumerable<SerialPortCandidate> ReadRegistryPortCandidates(HashSet<string> availablePorts)
        {
            using var enumKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum");
            if (enumKey is null)
            {
                yield break;
            }

            foreach (var rootName in new[] { "USB", "FTDIBUS" })
            {
                using var rootKey = OpenSubKeySafely(enumKey, rootName);
                if (rootKey is null)
                {
                    continue;
                }

                foreach (var candidate in ReadRegistryPortCandidates(rootKey, rootName, availablePorts, 0))
                {
                    yield return candidate;
                }
            }
        }

        private static IEnumerable<SerialPortCandidate> ReadRegistryPortCandidates(
            Microsoft.Win32.RegistryKey registryKey,
            string hardwareId,
            HashSet<string> availablePorts,
            int depth)
        {
            if (depth > 5)
            {
                yield break;
            }

            string[] subKeyNames;
            try
            {
                subKeyNames = registryKey.GetSubKeyNames();
            }
            catch (IOException)
            {
                yield break;
            }
            catch (UnauthorizedAccessException)
            {
                yield break;
            }
            catch (System.Security.SecurityException)
            {
                yield break;
            }

            foreach (var subKeyName in subKeyNames)
            {
                using var subKey = OpenSubKeySafely(registryKey, subKeyName);
                if (subKey is null)
                {
                    continue;
                }

                var childHardwareId = $"{hardwareId}\\{subKeyName}";
                using var deviceParametersKey = OpenSubKeySafely(subKey, "Device Parameters");
                var portName = deviceParametersKey?.GetValue("PortName") as string;

                if (!string.IsNullOrWhiteSpace(portName) && availablePorts.Contains(portName))
                {
                    var friendlyName = GetRegistryStringValue(subKey, "FriendlyName")
                        ?? GetRegistryStringValue(subKey, "DeviceDesc")
                        ?? string.Empty;
                    yield return new SerialPortCandidate(
                        portName,
                        friendlyName,
                        childHardwareId,
                        ScoreEspCandidate(friendlyName, childHardwareId));
                }

                foreach (var candidate in ReadRegistryPortCandidates(subKey, childHardwareId, availablePorts, depth + 1))
                {
                    yield return candidate;
                }
            }
        }

        private static Microsoft.Win32.RegistryKey? OpenSubKeySafely(Microsoft.Win32.RegistryKey registryKey, string subKeyName)
        {
            try
            {
                return registryKey.OpenSubKey(subKeyName);
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
            catch (System.Security.SecurityException)
            {
                return null;
            }
        }

        private static string? GetRegistryStringValue(Microsoft.Win32.RegistryKey registryKey, string valueName)
        {
            try
            {
                return registryKey.GetValue(valueName) as string;
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
            catch (System.Security.SecurityException)
            {
                return null;
            }
        }

        private static int ScoreEspCandidate(string friendlyName, string hardwareId)
        {
            var text = $"{friendlyName} {hardwareId}".ToUpperInvariant();
            var score = 0;

            if (ContainsAny(text, "VID_303A", "ESPRESSIF", "ESP32", "ESP8266", "USB JTAG/SERIAL DEBUG UNIT"))
            {
                score += 100;
            }

            if (ContainsAny(text, "VID_10C4&PID_EA60", "VID_10C4&PID_EA70", "CP210", "SILICON LABS"))
            {
                score += 80;
            }

            if (ContainsAny(text, "VID_1A86", "VID_1A86&PID_7523", "VID_1A86&PID_55D3", "VID_1A86&PID_55D4", "CH340", "CH343", "CH344", "CH9102", "WCH"))
            {
                score += 80;
            }

            if (ContainsAny(text, "VID_0403", "FTDI"))
            {
                score += 50;
            }

            if (ContainsAny(text, "USB-SERIAL", "USB SERIAL", "USB SERIAL DEVICE", "USB-ENHANCED-SERIAL"))
            {
                score += 20;
            }

            if (ContainsAny(text, "BLUETOOTH"))
            {
                score -= 100;
            }

            return Math.Max(0, score);
        }

        private static bool ContainsAny(string text, params string[] values)
        {
            foreach (var value in values)
            {
                if (text.Contains(value, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static int GetPortNumber(string portName)
        {
            return portName.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(portName[3..], out var portNumber)
                ? portNumber
                : int.MaxValue;
        }

        private sealed class SerialPortCandidate
        {
            public SerialPortCandidate(string portName, string friendlyName, string hardwareId, int score)
            {
                PortName = portName;
                FriendlyName = friendlyName;
                HardwareId = hardwareId;
                Score = score;
            }

            public string PortName { get; }

            public string FriendlyName { get; }

            public string HardwareId { get; }

            public int Score { get; }

            public string DisplayText => string.IsNullOrWhiteSpace(FriendlyName)
                ? PortName
                : $"{PortName} ({FriendlyName})";
        }

        public void Open()
        {
            lock (_portLock)
            {
                if (!_port.IsOpen)
                {
                    _port.Open();
                    TryClearBuffers();
                }
            }
        }

        public void Close()
        {
            lock (_portLock)
            {
                if (_port.IsOpen)
                {
                    _port.Close();
                }
            }
        }

        public void ClearBuffers() => TryClearBuffers();

        public async Task WriteLineAsync(string payload, CancellationToken cancellationToken = default)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(payload + _port.NewLine);

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
                    catch (IOException ex) when (ex is not SerialWriteTimedOutException && attempt < IoAttemptCount)
                    {
                        TryClearOutputBuffer();
                        await Task.Delay(IoRetryDelay, cancellationToken).ConfigureAwait(false);
                    }
                    catch (TimeoutException) when (attempt < IoAttemptCount)
                    {
                        TryClearOutputBuffer();
                        await Task.Delay(IoRetryDelay, cancellationToken).ConfigureAwait(false);
                    }
                    catch (TimeoutException ex)
                    {
                        throw new IOException(
                            $"A soros port {IoAttemptCount} próbálkozás után sem tudta elküldeni az adatot {WriteTimeoutMilliseconds} ms-os írási időkorláttal.",
                            ex);
                    }
                }
            }
            finally
            {
                _writeLock.Release();
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

            var writeTask = WriteStreamAsync(stream, bytes, cancellationToken);
            var timeoutTask = Task.Delay(WriteTimeoutMilliseconds, cancellationToken);
            var completedTask = await Task.WhenAny(writeTask, timeoutTask).ConfigureAwait(false);

            if (completedTask == writeTask)
            {
                await writeTask.ConfigureAwait(false);
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            Close();
            ObserveFault(writeTask);

            throw new SerialWriteTimedOutException(
                $"A soros port {WriteTimeoutMilliseconds} ms alatt nem vette át az írandó adatot, ezért a kapcsolat le lett zárva.");
        }

        private static async Task WriteStreamAsync(Stream stream, byte[] bytes, CancellationToken cancellationToken)
        {
            try
            {
                await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (TimeoutException ex)
            {
                throw new SerialWriteTimedOutException("A soros port stream írása időkorlátba ütközött.", ex);
            }
        }

        private static void ObserveFault(Task task)
        {
            _ = task.ContinueWith(
                completedTask => _ = completedTask.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        // A blokkoló ReadLine (akár ReadTimeout ideig) NEM fut a _portLock alatt: különben az írás
        // (WriteRawAsync) és a lezárás minden olvasási ciklusban akár 200 ms-ot várna, sőt ki is
        // éhezhetne. A SerialPort egyidejű olvasást és írást megenged; ha olvasás közben zárják le
        // a portot, a ReadLine kivételt dob, amit az alábbi catch ágak kezelnek.
        public Task<string> ReadLineAsync(CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        lock (_portLock)
                        {
                            if (!_port.IsOpen)
                            {
                                return string.Empty;
                            }
                        }

                        return _port.ReadLine().TrimEnd('\r');
                    }
                    catch (TimeoutException)
                    {
                    }
                    catch (ObjectDisposedException)
                    {
                        return string.Empty;
                    }
                    catch (InvalidOperationException)
                    {
                        return string.Empty;
                    }
                    catch (IOException)
                    {
                        Thread.Sleep(100);
                    }
                }

                return string.Empty;
            }, CancellationToken.None);
        }

        public void Dispose()
        {
            _writeLock.Dispose();
            _port.Dispose();
        }

        private void TryClearBuffers()
        {
            TryClearInputBuffer();
            TryClearOutputBuffer();
        }

        private void TryClearInputBuffer()
        {
            try
            {
                lock (_portLock)
                {
                    if (_port.IsOpen)
                    {
                        _port.DiscardInBuffer();
                    }
                }
            }
            catch (InvalidOperationException)
            {
            }
            catch (IOException)
            {
            }
        }

        private void TryClearOutputBuffer()
        {
            try
            {
                lock (_portLock)
                {
                    if (_port.IsOpen)
                    {
                        _port.DiscardOutBuffer();
                    }
                }
            }
            catch (InvalidOperationException)
            {
            }
            catch (IOException)
            {
            }
        }
        private sealed class SerialWriteTimedOutException : IOException
        {
            public SerialWriteTimedOutException(string message)
                : base(message)
            {
            }

            public SerialWriteTimedOutException(string message, Exception innerException)
                : base(message, innerException)
            {
            }
        }
    }
}
