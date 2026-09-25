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

/// <summary>
/// The token acquisition and data boundary probe that run before the agent is created.
/// </summary>
/// <remarks>
/// A seam, because the recovery entry point around it owns the console, static service
/// initialization, real MSAL, the spinner and a process spawn, and none of this behaviour can be
/// driven through it.
/// </remarks>
internal static class AgentStartupBoundary
{
    /// <summary>
    /// Acquires the Azure token for the builtin provider and probes the caller tenant's ARM data
    /// boundary.
    /// </summary>
    /// <remarks>
    /// <b>Never throws.</b> Failures are classified into <see cref="StartupBoundary.Boundary"/> and
    /// carried out in <see cref="StartupBoundary.Failure"/> for the caller to re-throw after it has
    /// recorded the outcome. That is what lets the caller record the boundary unconditionally, with
    /// no path around the write: returning a failure instead of throwing it keeps the original
    /// exception and its stack intact while still leaving a value to record, and it keeps this
    /// method testable through its return value rather than through plumbed exceptions.
    /// </remarks>
    internal static async Task<StartupBoundary> ResolveAsync(
        AgentOptions options,
        IAuthService authService,
        IProfileService profileService,
        Func<IProfileService, IDataBoundaryResolver> resolverFactory,
        Activity? session,
        CancellationToken ct)
    {
        // The provider decision lives inside the seam rather than at the call site. Outside it, the
        // guarantee that a github session never constructs a resolver would be production code no
        // test of this seam could reach, so the test would only ever assert against a copy of the
        // branch.
        //
        // github authenticates the model with the user's own Copilot credential and never touches
        // ARM, so asking those users for an Azure login would be a regression; anything else is a
        // value startup rejects moments later.
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

        // Past this line the token is real, so nothing below may be recorded as an auth failure and
        // the token must reach the caller whatever happens. A single try around both halves made an
        // ARM or factory fault report `no_token` and threw the acquired token away, so the caller
        // re-acquired one it already had.
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
            // The resolver contracts never to throw, so reaching here means that contract broke —
            // our bug, and not a reason to deny recovery the user already authenticated for. The
            // boundary is observation-only today, so it is recorded as faulted and startup goes on;
            // no failure is handed back to re-raise.
            return new StartupBoundary(
                DataBoundaryResult.NoAnswer(DataBoundaryUnavailable.LookupFaulted), auth, null);
        }
    }

    /// <summary>
    /// The boundary recorded when no lookup is made, i.e. the provider is not <c>builtin</c>.
    /// </summary>
    /// <remarks>
    /// Two distinct reasons, not one. <c>github</c> deliberately skips ARM; anything else is a
    /// configuration value that startup rejects moments later. Because the boundary is recorded
    /// before that rejection happens, collapsing them would file every provider typo in telemetry
    /// as a GitHub session that ran — a claim that was never true.
    /// </remarks>
    internal static DataBoundaryResult SkippedBoundaryFor(AgentOptions options) =>
        DataBoundaryResult.NoAnswer(
            options.IsGithubProvider
                ? DataBoundaryUnavailable.SkippedGithub
                : DataBoundaryUnavailable.SkippedUnsupportedProvider);
}
