using ReactionTimeMeasurementApp.Diagnostics;
using ReactionTimeMeasurementApp.Domain;
using ReactionTimeMeasurementApp.Measurement;

namespace ReactionTimeMeasurementApp.Infrastructure.Esp;

// Kapcsolódás az ESP-hez soros porton. A port a beállításokból jön, vagy (ha ott üres) az
// USB-eszközazonosítók alapján automatikusan kerül kiválasztásra.
internal sealed class EspResponseDeviceConnector : IResponseDeviceConnector
{
    private const string UnknownPortName = "ismeretlen COM-port";

    public async Task<IResponseDevice> ConnectAsync(HardwareSettings settings, CancellationToken cancellationToken)
    {
        var portName = string.IsNullOrWhiteSpace(settings.EspPortName) ? null : settings.EspPortName.Trim();
        Serial? serial = null;

        try
        {
            portName ??= Serial.GetPreferredEspPortName();
            serial = new Serial(portName, settings.EspBaudRate);
            serial.MessageReceived += message => Logger.Info($"[SERIAL][IN] '{message}'");
            await serial.StartAsync(cancellationToken);
            Logger.Info($"[SERIAL] Port megnyitva: {portName} @ {settings.EspBaudRate}");
            return new EspResponseDevice(serial, portName, settings.EspBaudRate);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (serial is not null)
            {
                await serial.DisposeAsync();
            }

            throw new ResponseDeviceConnectionException(portName ?? UnknownPortName, ex);
        }
    }
}

// Az ESP a numerikus scanout protokollal (lásd docs\SCANOUT_PROTOCOL.md): a PC elküldi a próba
// scanout-korrekcióját, az ESP visszaküldi a korrigált reakcióidőt mikroszekundumban.
internal sealed class EspResponseDevice : IResponseDevice
{
    private readonly Serial _serial;

    public EspResponseDevice(Serial serial, string portName, int baudRate)
    {
        _serial = serial;
        PortName = portName;
        BaudRate = baudRate;
    }

    public string PortName { get; }

    public int BaudRate { get; }

    public Task SendScanoutCorrectionAsync(string correctionMicroseconds, CancellationToken cancellationToken)
    {
        return _serial.SendAsync(correctionMicroseconds, cancellationToken);
    }

    // Csak az értelmezhető reakcióidő-sort fogadja el; időtúllépéskor a Serial üres szöveget ad
    // vissza, amiből null lesz.
    public async Task<double?> WaitForReactionTimeMsAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var response = await _serial.WaitForResponseAsync(
            message => ScanoutProtocol.TryParseReactionTime(message, out _),
            timeout,
            cancellationToken).ConfigureAwait(false);

        return ScanoutProtocol.TryParseReactionTime(response, out var reactionTimeMs) ? reactionTimeMs : null;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _serial.StopAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.Warning($"[SERIAL] A port lezárása közben hiba történt: {ex.Message}");
        }

        await _serial.DisposeAsync().ConfigureAwait(false);
    }
}
