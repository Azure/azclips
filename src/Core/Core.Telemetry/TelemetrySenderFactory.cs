using Core.Abstractions;

namespace Core.Telemetry;

/// <summary>
/// Creates the appropriate ITelemetrySender based on client type.
/// CLI: file-based buffering + background replay.
/// PowerShell: inline Azure Monitor export.
/// </summary>
public static class TelemetrySenderFactory
{
    public static ITelemetrySender Create(ClientType clientType) => clientType switch
    {
        ClientType.CLI => new FileTelemetrySender(),
        ClientType.PowerShell => new DirectTelemetrySender(),
        _ => new FileTelemetrySender()
    };
}
