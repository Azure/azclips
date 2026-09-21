// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

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
    /// Resolves the tenant and data boundary for <paramref name="profileNameOverride"/> (or the
    /// active profile, when null/empty). Best-effort: any failure (missing profile, timeout,
    /// token/network error) returns <c>(null, null)</c> rather than throwing, since missing
    /// routing metadata should fall back to Global on upload rather than block the command.
    /// </summary>
    public static async Task<(string? TenantId, string? Boundary)> ResolveAsync(
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
                return (null, null);

            var boundary = await DataBoundaryResolver.ResolveAndCacheAsync(
                tenantId,
                async tokenCancellationToken =>
                {
                    // Suppressed: this token acquisition's own MsalAuthService.AcquireTokenSilentlyAsync
                    // span would otherwise end (and export) before the boundary it's being used to
                    // discover is known, so AriaActivityExporter would fall back to routing it to
                    // Global even for an EU tenant. See TelemetrySuppressionScope's remarks.
                    using var _ = TelemetrySuppressionScope.Enter();
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

            return (tenantId, boundary);
        }
        catch
        {
            return (null, null);
        }
    }
}
