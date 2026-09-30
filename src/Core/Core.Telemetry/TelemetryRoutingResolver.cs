// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using OpenTelemetry;

namespace Core.Telemetry;

/// <summary>
/// Resolves the (tenantId, ARM data boundary) pair used to route telemetry for an invocation,
/// given an already-resolved profile-name override (or none, meaning "use the active profile").
/// </summary>
/// <remarks>
/// Deliberately takes <see cref="IProfileService"/>/<see cref="IAuthService"/> as explicit
/// parameters rather than resolving them from a service locator: <c>Core.Telemetry</c> sits
/// below <c>Core.DependencyInjection</c> in the project graph (the DI composition root
/// references this project to wire telemetry services, not the other way around), so this type
/// cannot depend on the service locator without creating a circular project reference. Callers
/// (CLI's <c>Program.Main</c>, and eventually other hosts) resolve the services themselves and
/// pass them in.
/// </remarks>
public static class TelemetryRoutingResolver
{
    /// <summary>
    /// Default timeout for the token-acquisition + data-boundary network round-trip. Shared by
    /// every caller so the resolution window (during which telemetry fails open to Global) stays
    /// consistent across the CLI's early routing pass and the engine's per-command resolution.
    /// </summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Resolves the tenant, data boundary, subscription, and auth method for
    /// <paramref name="profileNameOverride"/> (or the active profile, when null/empty).
    /// Best-effort: any failure (missing profile, timeout, token/network error) returns all
    /// <c>null</c> rather than throwing, since missing routing metadata should fall back to
    /// Global on upload rather than block the command. <c>SubscriptionId</c>/<c>AuthType</c>
    /// come straight off the already-resolved profile (no extra network call) so callers can
    /// tag them on the root before replaying startup-phase spans, the same way
    /// <c>TenantId</c>/<c>Boundary</c> already are — otherwise those two fields stay unset
    /// until the authoritative <c>ProcessorContextCreated</c> tagging fires during command
    /// processing, well after any replayed startup spans have already exported.
    /// </summary>
    public static async Task<(string? TenantId, string? Boundary, string? SubscriptionId, string? AuthType)> ResolveAsync(
        IProfileService profileService,
        IAuthService authService,
        string? profileNameOverride,
        CancellationToken cancellationToken,
        TimeSpan? timeout = null)
    {
        try
        {
            var profile = string.IsNullOrWhiteSpace(profileNameOverride)
                ? await profileService.GetActiveProfileAsync(cancellationToken)
                : await profileService.GetProfileAsync(profileNameOverride, cancellationToken);
            var tenantId = profile?.TenantId;
            if (string.IsNullOrWhiteSpace(tenantId))
                return (null, null, null, null);

            var subscriptionId = profile!.SubscriptionId;
            var authType = profile.AuthMethod.ToString();

            // Suppresses ALL instrumentation (both TelemetryService-created spans and OpenTelemetry's
            // HTTP auto-instrumentation) for the two network calls this makes (token acquisition, then
            // the ARM data-boundary query) — both would otherwise end and export before the boundary
            // they're used to discover is known, so AriaActivityExporter would fall back to routing
            // them to Global even for an EU tenant. See Engine.ResolveDataBoundaryAsync's remarks.
            using var _ = SuppressInstrumentationScope.Begin();
            var boundary = await DataBoundaryResolver.ResolveAndCacheAsync(
                tenantId,
                async tokenCancellationToken =>
                {
                    var result = await authService.AcquireTokenSilentlyAsync(
                        scopes: null,
                        tenantId: tenantId,
                        promptOnError: false,
                        cancellationToken: tokenCancellationToken,
                        profile: profile);
                    return result.AccessToken;
                },
                timeout ?? DefaultTimeout,
                cancellationToken);

            return (tenantId, boundary, subscriptionId, authType);
        }
        catch
        {
            return (null, null, null, null);
        }
    }
}
