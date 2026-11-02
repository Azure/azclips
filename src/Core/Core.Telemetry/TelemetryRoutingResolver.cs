// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using OpenTelemetry;

namespace Core.Telemetry;

/// <summary>
/// Resolves profile identity and tenant boundary outcomes for telemetry routing.
/// </summary>
public static class TelemetryRoutingResolver
{
    /// <summary>
    /// Shared budget for token acquisition and the ARM boundary lookup.
    /// </summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Boundary-only compatibility API. Telemetry callers use <see cref="ResolveRoutingAsync"/>
    /// to preserve the resolution outcome.
    /// </summary>
    public static async Task<(string? TenantId, string? Boundary, string? SubscriptionId, string? AuthType)> ResolveAsync(
        IProfileService profileService,
        IAuthService authService,
        string? profileNameOverride,
        CancellationToken cancellationToken,
        TimeSpan? timeout = null,
        ILoggingService? loggingService = null)
    {
        var result = await ResolveRoutingAsync(profileService, authService, profileNameOverride,
            cancellationToken, timeout, loggingService).ConfigureAwait(false);
        return (result.TenantId, result.Resolution.Boundary, result.SubscriptionId, result.AuthType);
    }

    /// <summary>
    /// Resolves the specified or active profile and preserves its identity when boundary lookup fails.
    /// Unavailable results use the regional policy; caller cancellation propagates.
    /// </summary>
    public static async Task<(string? TenantId, DataBoundaryResolution Resolution, string? SubscriptionId, string? AuthType)> ResolveRoutingAsync(
        IProfileService profileService,
        IAuthService authService,
        string? profileNameOverride,
        CancellationToken cancellationToken,
        TimeSpan? timeout = null,
        ILoggingService? loggingService = null)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var profile = string.IsNullOrWhiteSpace(profileNameOverride)
                ? await profileService.GetActiveProfileAsync(cancellationToken)
                : await profileService.GetProfileAsync(profileNameOverride, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var subscriptionId = profile?.SubscriptionId;
            var authType = profile?.AuthMethod.ToString();

            var tenantId = profile?.TenantId;
            if (string.IsNullOrWhiteSpace(tenantId))
                return (null, new(null, DataBoundaryOutcome.TenantUnavailable), subscriptionId, authType);

            // Do not export discovery calls before their routing outcome is known.
            using var _ = SuppressInstrumentationScope.Begin();
            var resolution = await DataBoundaryResolver.ResolveAsync(
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

            return (tenantId, resolution, subscriptionId, authType);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Logging is best-effort too: a throwing logger must not replace the fallback below.
            try
            {
                var profileLabel = string.IsNullOrWhiteSpace(profileNameOverride) ? "active" : profileNameOverride;
                if (loggingService is not null)
                    loggingService.Debug($"[{nameof(TelemetryRoutingResolver)}] Failed to resolve telemetry routing for profile '{profileLabel}': {ex.Message}");
                else
                    Console.Error.WriteLine($"[{nameof(TelemetryRoutingResolver)}] Profile resolution failed: {ex.GetType().Name}.");
            }
            catch
            {
            }
            return (null, new(null, DataBoundaryOutcome.TenantUnavailable), null, null);
        }
    }
}
