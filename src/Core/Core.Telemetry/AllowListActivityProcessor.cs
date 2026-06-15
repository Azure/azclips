using System.Diagnostics;
using Core.Abstractions;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Core.Telemetry;

/// <summary>
/// Filters Activities and their tags.
///
/// Activity filtering: non-allowed activities still execute normally
/// (parent context, in-process listeners and events, etc) but the
/// <see cref="ActivityTraceFlags.Recorded"/> flag is cleared so downstream
/// export processors skip them.
///
/// Tag filtering: on activities that ARE allowed, any tag whose key is not
/// in the provided tag allowlist is stripped before export. Passing a null
/// tag allowlist disables tag filtering entirely (legacy behaviour).
/// </summary>
internal sealed class AllowListActivityProcessor : BaseProcessor<Activity>
{
    private readonly HashSet<string> _allowedOperationNames;
    private readonly HashSet<string>? _allowedTagKeys;

    public AllowListActivityProcessor(IEnumerable<string> allowedOperationNames)
        : this(allowedOperationNames, allowedTagKeys: null)
    {
    }

    public AllowListActivityProcessor(
        IEnumerable<string> allowedOperationNames,
        IEnumerable<string>? allowedTagKeys)
    {
        _allowedOperationNames = new HashSet<string>(allowedOperationNames, StringComparer.Ordinal);
        _allowedTagKeys = allowedTagKeys is null
            ? null
            : new HashSet<string>(allowedTagKeys, StringComparer.Ordinal);
    }

    public override void OnEnd(Activity data)
    {
        if (!_allowedOperationNames.Contains(data.OperationName))
        {
            data.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
#if DEBUG
            Console.Error.WriteLine($"[TELEMETRY-BLOCK] {data.OperationName}");
#endif
            return;
        }

        StripDisallowedTags(data);

#if DEBUG
        Console.Error.WriteLine($"[TELEMETRY-ALLOW] {data.OperationName}");
#endif
    }

    /// <summary>
    /// Removes any tag from <paramref name="data"/> whose key is not in the
    /// configured tag allowlist. Setting a tag value to <c>null</c> via
    /// <see cref="Activity.SetTag(string, object?)"/> deletes the tag, per
    /// the documented .NET 7+ behaviour.
    /// </summary>
    private void StripDisallowedTags(Activity data)
    {
        if (_allowedTagKeys is null) return;

        // Snapshot the keys first; SetTag mutates the underlying collection
        // which would otherwise invalidate the enumerator.
        List<string>? toRemove = null;
        foreach (var tag in data.TagObjects)
        {
            if (_allowedTagKeys.Contains(tag.Key)) continue;
            (toRemove ??= new List<string>()).Add(tag.Key);
        }

        if (toRemove is null) return;

        foreach (var key in toRemove)
        {
            data.SetTag(key, null);
#if DEBUG
            Console.Error.WriteLine($"[TELEMETRY-STRIP-TAG] {data.OperationName} -> {key}");
#endif
        }
    }
}

/// <summary>
/// Single source of truth for telemetry allowlists. Anything not listed in
/// <see cref="Names"/> is dropped at export; on activities that ARE exported,
/// any tag whose key is not in <see cref="TagKeys"/> is also stripped.
/// </summary>
public static class TelemetryAllowList
{
    /// <summary>
    /// Operation (activity) names that may be exported. Per-product allowlist
    /// reviewed for privacy.
    /// </summary>
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

    /// <summary>
    /// Tag keys that may be exported on allowed activities. Anything not in
    /// this set is stripped at export time.
    ///
    /// NOTE: <see cref="TelemetryTags.Id"/> is intentionally EXCLUDED. It is
    /// a SHA-256 of (primary MAC + machine name), which under GDPR / Microsoft
    /// data-classification guidance is pseudonymized personal data (EUII) — the
    /// inputs are themselves identifying and the hash is trivially re-identifiable
    /// because the input space is too small. Do not add it back without a
    /// privacy review.
    /// </summary>
    public static readonly IReadOnlyCollection<string> TagKeys = new HashSet<string>(StringComparer.Ordinal)
    {
        // Per-process invariants safe to emit.
        TelemetryTags.IsMicrosoftInternal, //TODO - we can likely remove this
        TelemetryTags.ProductName,
        TelemetryTags.ProductVersion,
        TelemetryTags.SchemaVersion,
        TelemetryTags.TelemetryVersion,
        TelemetryTags.ClientType,
        TelemetryTags.EventName,                
        TelemetryTags.Command,
        TelemetryTags.ModuleName,
        TelemetryTags.ModuleVersion,
        TelemetryTags.Parameter, //TODO this needs redaction
    };

    public static TracerProviderBuilder AddTelemetryAllowListFilter(this TracerProviderBuilder builder)
        => builder.AddProcessor(new AllowListActivityProcessor(Names, TagKeys));
}

