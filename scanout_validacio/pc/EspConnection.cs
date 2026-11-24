using System.Collections.Concurrent;
using System.Globalization;
using System.IO.Ports;

namespace ScanoutValidacio;

// Soronkénti szöveges kapcsolat a validációs firmware-rel (firmware/main/main.c). A port kezelése a
// reakcióidő-mérőével azonos: 8N1, DTR/RTS beállítás nélkül, megnyitás után 1,5 s várakozás (az ESP
// újraindulhat), a pufferek ürítése. Minden hívás blokkoló; a felület háttérszálról hívja.
internal sealed class EspConnection : IDisposable
{
    public const string VersionPrefix = "SV-";

    private readonly SerialPort _port;
    private readonly BlockingCollection<string> _lines = new();
    private readonly Thread _reader;
    private volatile bool _closed;

    private EspConnection(SerialPort port)
    {
        _port = port;
        _reader = new Thread(ReadLoop) { IsBackground = true, Name = "ESP olvasó" };
        _reader.Start();
    }

    public string PortName => _port.PortName;
    public string FirmwareVersion { get; private set; } = "";

    public static EspConnection Connect(string portName, int baudRate)
    {
        if (!string.IsNullOrWhiteSpace(portName))
        {
            return TryOpen(portName, baudRate, out var error)
                ?? throw new InvalidOperationException($"A(z) {portName} porton nem válaszol a validációs firmware: {error}");
        }

        var errors = new List<string>();
        foreach (var name in SerialPort.GetPortNames().OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            var connection = TryOpen(name, baudRate, out var error);
            if (connection is not null)
            {
                return connection;
            }

            errors.Add($"{name}: {error}");
        }

        throw new InvalidOperationException(
            "Nem található a validációs firmware egyik soros porton sem."
            + (errors.Count > 0 ? Environment.NewLine + string.Join(Environment.NewLine, errors) : " (nincs soros port)"));
    }

    private static EspConnection? TryOpen(string portName, int baudRate, out string error)
    {
        SerialPort? port = null;
        EspConnection? connection = null;
        try
        {
            port = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
            {
                NewLine = "\n",
                ReadTimeout = SerialPort.InfiniteTimeout,
                WriteTimeout = 1000,
            };
            port.Open();
            Thread.Sleep(1500);
            port.DiscardInBuffer();
            port.DiscardOutBuffer();
            connection = new EspConnection(port);

            var ok = false;
            for (var i = 0; i < 3 && !ok; i++)
            {
                ok = connection.Request("CHECK", l => l == "OK", 1000) is not null;
            }

            if (!ok)
            {
                error = "nincs OK válasz a CHECK-re";
                connection.Dispose();
                return null;
            }

            var version = connection.Request("VERSION", l => l.Count(c => c == '.') == 2, 500);
            if (version is null || !version.StartsWith(VersionPrefix, StringComparison.Ordinal))
            {
                error = version is null
                    ? "nincs válasz a VERSION-re"
                    : $"a firmware verziója {version}: ez nem a validációs firmware (a reakcióidő-mérőé?)";
                connection.Dispose();
                return null;
            }

            connection.FirmwareVersion = version;
            error = "";
            return connection;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or TimeoutException)
        {
            error = ex.Message;
            if (connection is not null)
            {
                connection.Dispose();
            }
            else
            {
                port?.Dispose();
            }

            return null;
        }
    }

    // Elküld egy parancsot, és az első olyan sort adja vissza, amelyre az accept igaz; időtúllépésnél
    // null. A korábban érkezett, fel nem dolgozott sorokat eldobja.
    public string? Request(string command, Func<string, bool> accept, int timeoutMs)
    {
        while (_lines.TryTake(out _))
        {
        }

        _port.Write(command + "\n");
        var deadline = Environment.TickCount64 + timeoutMs;
        while (true)
        {
            var remaining = deadline - Environment.TickCount64;
            if (remaining <= 0 || !_lines.TryTake(out var line, (int)remaining))
            {
                return null;
            }

            if (accept(line))
            {
                return line;
            }
        }
    }

    public bool Arm() => Request("ARM", l => l == "ARMED", 500) is not null;

    public void Stop() => Request("STOP", l => l == "STOPPED", 500);

    public (int Reference, int Measurement)? SetThresholds(int reference, int measurement)
    {
        var reply = Request($"THR {reference} {measurement}", l => l.StartsWith("THR ", StringComparison.Ordinal), 500);
        var parts = reply?.Split(' ');
        return parts is { Length: 3 } && int.TryParse(parts[1], out var a) && int.TryParse(parts[2], out var b)
            ? (a, b)
            : null;
    }

    // Az utolsó ADC-kérésre kapott nyers sor (hibakereséshez; "nincs válasz" időtúllépésnél).
    public string LastAdcReply { get; private set; } = "";

    public (int Reference, int Measurement)? ReadAdc()
    {
        var reply = Request("ADC", l => l == "NA" || l.StartsWith("ADC ", StringComparison.Ordinal), 500);
        LastAdcReply = reply ?? "nincs válasz";
        var parts = reply?.Split(' ');
        return parts is { Length: 3 } && int.TryParse(parts[1], out var a) && int.TryParse(parts[2], out var b)
            ? (a, b)
            : null;
    }

    // "RES dt=... t_ref=... ..." vagy "RES NA ref=0 mero=1 ..." -> kulcs-érték párok; az "NA"
    // esetén a "NA" kulcs is szerepel.
    public Dictionary<string, string>? ReadResult()
    {
        var reply = Request("RESULT", l => l.StartsWith("RES ", StringComparison.Ordinal), 500);
        if (reply is null)
        {
            return null;
        }

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var token in reply.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1))
        {
            var eq = token.IndexOf('=');
            if (eq < 0)
            {
                result[token] = "";
            }
            else
            {
                result[token[..eq]] = token[(eq + 1)..];
            }
        }

        return result;
    }

    private void ReadLoop()
    {
        while (!_closed)
        {
            try
            {
                var line = _port.ReadLine().Trim();
                if (line.Length > 0)
                {
                    _lines.Add(line);
                }
            }
            catch (Exception) when (_closed)
            {
                return;
            }
            catch (TimeoutException)
            {
            }
            catch (Exception)
            {
                return;
            }
        }
    }

    public void Dispose()
    {
        _closed = true;
        try
        {
            _port.Close();
        }
        catch (IOException)
        {
        }

        _port.Dispose();
    }
}

internal static class Inv
{
    public static string F(double value, string format = "0.###") => value.ToString(format, CultureInfo.InvariantCulture);

    public static double D(string value) => double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
}
