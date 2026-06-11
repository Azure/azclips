using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Core.Telemetry;

/// <summary>
/// Filters Activities
/// Non-allowed activities
/// still execute normally (parent context, in-process listeners and events, etc)
/// but the ActivityTraceFlags.Recorded flag is cleared so downstream
/// export processors skip them.
/// </summary>
internal sealed class AllowListActivityProcessor : BaseProcessor<Activity>
{
    private readonly HashSet<string> _allowed;

    public AllowListActivityProcessor(IEnumerable<string> allowedOperationNames) => _allowed = new HashSet<string>(allowedOperationNames, StringComparer.Ordinal);

    public override void OnEnd(Activity data)
    {
        if (_allowed.Contains(data.OperationName))
        {
#if DEBUG
            Console.Error.WriteLine($"[TELEMETRY-ALLOW] {data.OperationName}");
#endif
            return;
        }

        data.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
#if DEBUG
        Console.Error.WriteLine($"[TELEMETRY-BLOCK] {data.OperationName}");
#endif
    }
}

/// <summary>
/// Single source of truth for telemetry allowlist.
/// Add operation names here to expose them to telemetry exporters
/// anything not listed is filtered out by/// 
/// </summary>
public static class TelemetryAllowList
{
    //TODO: Filter/Redact tags also, not just the operation names
    public static readonly IReadOnlyCollection<string> Names = new HashSet<string>(StringComparer.Ordinal)
    {
        //Core
        "AzureCLI.ProcessCommand",
        "CLI.Activity.Root",
        "PowerShell.Activity.Root",
        "TUI.Activity.Root",
        "ProcessorProcessAsyncStart",

        //Identity
        "MsalAuthService.LoginAsync",
        "MsalAuthService.LogoutAsync",
        "MsalAuthService.AcquireTokenSilentAsync",
        "MsalAuthService.AcquireTokenInteractiveAsync",
        "MsalAuthService.AcquireTokenByDeviceCodeAsync",
        "MsalAuthService.AcquireTokenSilentlyAsync",

        //Extensions
        "AddExtensionProcessor.Process",
    };

    public static TracerProviderBuilder AddTelemetryAllowListFilter(this TracerProviderBuilder builder)
        => builder.AddProcessor(new AllowListActivityProcessor(Names));
}
