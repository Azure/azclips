using System.Diagnostics;
using System.Runtime.ExceptionServices;
using Core.Abstractions;
using Core.Agent.Providers;

namespace Core.Agent.AgentMode;

/// <summary>What recovery startup learned before the agent was created.</summary>
/// <param name="DataBoundary">The tenant's ARM data boundary, or null when it is not known.</param>
/// <param name="Auth">
/// The Azure token the builtin provider authenticates with, or null when none was acquired —
/// either because the provider needs none or because acquiring it failed.
/// </param>
/// <param name="Failure">
/// The exception the caller must re-raise once it has recorded the outcome, captured with its
/// original stack, or null when startup succeeded.
/// </param>
internal readonly record struct StartupBoundary(
    string? DataBoundary,
    AuthResult? Auth,
    ExceptionDispatchInfo? Failure);

/// <summary>Resolves the boundary lookup.</summary>
/// <param name="tenantId">The tenant to look up.</param>
/// <param name="accessToken">The token already acquired for the builtin provider.</param>
internal delegate Task<string?> DataBoundaryLookup(string tenantId, string accessToken, CancellationToken ct);

/// <summary>Coordinates token acquisition and the data boundary lookup before Agent creation.</summary>
internal static class AgentStartupBoundary
{
    /// <summary>Ceiling for the boundary lookup on the recovery startup path.</summary>
    internal static readonly TimeSpan LookupTimeout = TimeSpan.FromSeconds(3);

    /// <summary>Acquires a builtin token and resolves the active tenant's data boundary.</summary>
    /// <remarks>
    /// Uses the boundary the CLI handed over when present. Otherwise asks Core, which reads
    /// its cache and calls ARM only on a miss. Non-builtin providers skip both.
    /// </remarks>
    internal static async Task<StartupBoundary> ResolveAsync(
        AgentOptions options,
        IAuthService authService,
        IProfileService profileService,
        string? handedOverBoundary,
        DataBoundaryLookup lookup,
        Activity? session,
        CancellationToken ct)
    {
        // GitHub uses Copilot credentials; do not require Azure login or call ARM here.
        if (!options.IsBuiltinProvider)
        {
            return new StartupBoundary(Known(handedOverBoundary), null, null);
        }

        var tokenWatch = Stopwatch.StartNew();
        AuthResult auth;
        try
        {
            auth = await BuiltinProviderConfig.AcquireAsync(options.Builtin, authService, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return new StartupBoundary(Known(handedOverBoundary), null, ExceptionDispatchInfo.Capture(ex));
        }
        finally
        {
            // Measures the acquisition alone, on every path.
            tokenWatch.Stop();
            session?.SetTag(TelemetryTags.AgentTokenElapsedMs, tokenWatch.ElapsedMilliseconds);
        }

        if (Known(handedOverBoundary) is { } known)
        {
            return new StartupBoundary(known, auth, null);
        }

        // Preserve the acquired token even if the lookup fails.
        try
        {
            var profile = await profileService.GetActiveProfileAsync(ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(profile?.TenantId))
            {
                return new StartupBoundary(null, auth, null);
            }

            var boundary = await lookup(profile.TenantId, auth.AccessToken, ct).ConfigureAwait(false);
            // Core's resolver returns null on any failure, including caller cancellation.
            ct.ThrowIfCancellationRequested();
            return new StartupBoundary(Known(boundary), auth, null);
        }
        catch (OperationCanceledException ex) when (ct.IsCancellationRequested)
        {
            return new StartupBoundary(null, auth, ExceptionDispatchInfo.Capture(ex));
        }
        catch (Exception)
        {
            // A failed lookup must not block recovery or discard the token.
            return new StartupBoundary(null, auth, null);
        }
    }

    /// <summary>Returns the value, or null when it carries no answer.</summary>
    private static string? Known(string? boundary) =>
        string.IsNullOrWhiteSpace(boundary) ||
        string.Equals(boundary, "Unknown", StringComparison.OrdinalIgnoreCase)
            ? null
            : boundary;
}
