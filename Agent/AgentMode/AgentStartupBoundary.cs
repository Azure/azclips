using System.Diagnostics;
using System.Runtime.ExceptionServices;
using Core.Abstractions;
using Core.Agent.Providers;

namespace Core.Agent.AgentMode;

/// <summary>What recovery startup learned before the agent was created.</summary>
/// <param name="Boundary">
/// The session's one data boundary value. Always present, on every outcome.
/// </param>
/// <param name="Auth">
/// The Azure token the builtin provider authenticates with, or null when none was acquired —
/// either because the provider needs none or because acquiring it failed.
/// </param>
/// <param name="Failure">
/// The exception the caller must re-raise once it has recorded the outcome, captured with its
/// original stack, or null when startup succeeded.
/// </param>
internal readonly record struct StartupBoundary(
    DataBoundaryResult Boundary,
    AuthResult? Auth,
    ExceptionDispatchInfo? Failure);

/// <summary>Coordinates token acquisition and the diagnostic boundary lookup before Agent creation.</summary>
internal static class AgentStartupBoundary
{
    /// <summary>Acquires a builtin token and probes the active tenant's ARM boundary.</summary>
    /// <remarks>
    /// Returns the boundary outcome and any failure with its original stack.
    /// The caller records the outcome before rethrowing the failure.
    /// Non-builtin providers skip token acquisition and ARM lookup.
    /// </remarks>
    internal static async Task<StartupBoundary> ResolveAsync(
        AgentOptions options,
        IAuthService authService,
        IProfileService profileService,
        Func<IProfileService, IDataBoundaryResolver> resolverFactory,
        Activity? session,
        CancellationToken ct)
    {
        // GitHub uses Copilot credentials; do not require Azure login or call ARM here.
        if (!options.IsBuiltinProvider)
        {
            return new StartupBoundary(SkippedBoundaryFor(options), null, null);
        }

        var tokenWatch = Stopwatch.StartNew();
        AuthResult auth;
        try
        {
            auth = await BuiltinProviderConfig.AcquireAsync(options.Builtin, authService, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (ct.IsCancellationRequested)
        {
            return new StartupBoundary(
                DataBoundaryResult.NoAnswer(DataBoundaryUnavailable.Cancelled),
                null,
                ExceptionDispatchInfo.Capture(ex));
        }
        catch (OperationCanceledException ex)
        {
            // Reached only when the caller did not cancel, which makes this a library's own
            // internal timeout — MSAL cancelling its own token — rather than the user. Without the
            // filter on the catch above, both arrive here as one event and every MSAL timeout is
            // recorded as a Ctrl+C.
            return new StartupBoundary(
                DataBoundaryResult.NoAnswer(DataBoundaryUnavailable.TimeoutToken),
                null,
                ExceptionDispatchInfo.Capture(ex));
        }
        catch (Exception ex)
        {
            return new StartupBoundary(
                DataBoundaryResult.NoAnswer(DataBoundaryUnavailable.NoToken),
                null,
                ExceptionDispatchInfo.Capture(ex));
        }
        finally
        {
            // Stops here on every path, so the tag measures the acquisition alone — a dead refresh
            // token going to the network before giving up is exactly the cost worth seeing. The
            // lookup that follows is timed separately on its own span.
            tokenWatch.Stop();
            session?.SetTag(TelemetryTags.RecoveryTokenElapsedMs, tokenWatch.ElapsedMilliseconds);
        }

        // Preserve the acquired token even if the diagnostic lookup fails.
        // Keep lookup failures separate from authentication failures.
        try
        {
            using var resolver = resolverFactory(profileService);
            var boundary = await resolver.ResolveAsync(auth.AccessToken, ct).ConfigureAwait(false);
            return new StartupBoundary(boundary, auth, null);
        }
        catch (OperationCanceledException ex) when (ct.IsCancellationRequested)
        {
            return new StartupBoundary(
                DataBoundaryResult.NoAnswer(DataBoundaryUnavailable.Cancelled),
                auth,
                ExceptionDispatchInfo.Capture(ex));
        }
        catch (Exception)
        {
            // Classify resolver faults without discarding the token or blocking recovery.
            return new StartupBoundary(
                DataBoundaryResult.NoAnswer(DataBoundaryUnavailable.LookupFaulted), auth, null);
        }
    }

    /// <summary>Distinguishes a GitHub skip from an unsupported provider.</summary>
    internal static DataBoundaryResult SkippedBoundaryFor(AgentOptions options) =>
        DataBoundaryResult.NoAnswer(
            options.IsGithubProvider
                ? DataBoundaryUnavailable.SkippedGithub
                : DataBoundaryUnavailable.SkippedUnsupportedProvider);
}
