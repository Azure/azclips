using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using Core.Abstractions;

namespace Core.Agent.Providers;

/// <summary>
/// Resolves the active profile tenant's ARM data boundary at recovery startup.
/// </summary>
/// <remarks>
/// <para>Today this only observes: the result is recorded and nothing branches on it. It lands
/// ahead of the endpoint move so that, by the time routing needs the value, we already know the
/// call works and what it costs.</para>
///
/// <para>ARM is the authoritative source. The boundary is a per-tenant setting, so it cannot be
/// inferred from the user's IP, a resource's location, or a subscription's region, and the
/// <c>tenant_region_scope</c> token claim is not a supported contract for it.</para>
///
/// <para>Follows the same shape as <c>AvailableLocationsTool</c> and <c>ArmCacheRefresher</c>:
/// the agent talks to ARM through <see cref="IAuthService"/> and an injectable
/// <see cref="HttpClient"/>, so nothing in Core has to change.</para>
/// </remarks>
internal sealed class DataBoundaryResolver
{
    private const string ArmEndpoint = "https://management.azure.com";

    // Tenant scope takes no prefix, so the path starts at /providers. Verified against ARM:
    // 2024-08-01 is the documented GA version and returns
    // { "name": "<tenantId>", "properties": { "dataBoundary": "Global", ... } }.
    private const string ApiVersion = "2024-08-01";
    private const string DataBoundaryPath = "/providers/Microsoft.Resources/dataBoundaries/default";

    private static readonly string[] ArmScope = { "https://management.azure.com/.default" };

    /// <summary>
    /// Ceiling for the whole lookup. Enforced with a linked token so it holds even when the
    /// injected client has a longer timeout, because this sits on the startup path of every
    /// recovery and must not be able to stall it.
    /// </summary>
    internal static readonly TimeSpan LookupTimeout = TimeSpan.FromSeconds(5);

    private readonly IAuthService _authService;
    private readonly IProfileService _profileService;
    private readonly HttpClient _http;

    public DataBoundaryResolver(IAuthService authService, IProfileService profileService, HttpClient? http = null)
    {
        _authService = authService;
        _profileService = profileService;
        _http = http ?? new HttpClient { Timeout = LookupTimeout };
    }

    /// <summary>
    /// Resolves the boundary for the active profile's tenant.
    /// </summary>
    /// <remarks>
    /// Returns <see cref="DataBoundary.Unknown"/> with a reason for every failure rather than
    /// throwing. Only a cancellation raised by <paramref name="ct"/> propagates, so Ctrl+C still
    /// exits promptly. Recovery is expected to continue no matter what this returns.
    /// </remarks>
    public Task<DataBoundaryResult> ResolveAsync(CancellationToken ct = default) =>
        ResolveWithTelemetryAsync(presuppliedToken: null, ct);

    /// <summary>
    /// Resolves the boundary using a token the caller already holds.
    /// </summary>
    /// <remarks>
    /// The builtin provider acquires an ARM-audience token for the model call anyway, so reusing it
    /// here keeps the recovery startup path to a single MSAL flow. That matters beyond the saved
    /// round trip: <c>MsalAuthService</c> builds a fresh <c>IPublicClientApplication</c> and
    /// re-registers the on-disk token cache on every acquisition, with no synchronization, so two
    /// concurrent acquisitions over one cache file is not a tested configuration.
    /// </remarks>
    public Task<DataBoundaryResult> ResolveAsync(string accessToken, CancellationToken ct = default) =>
        ResolveWithTelemetryAsync(accessToken, ct);

    private async Task<DataBoundaryResult> ResolveWithTelemetryAsync(string? presuppliedToken, CancellationToken ct)
    {
        using var activity = AgentTracing.Source.StartActivity("Agent.DataBoundary");
        var stopwatch = Stopwatch.StartNew();

        var result = await ResolveCoreAsync(activity, presuppliedToken, ct).ConfigureAwait(false);

        stopwatch.Stop();
        // Tag keys come from TelemetryTags so they ride the vetted properties.* channel and are
        // covered by IsAllowed_EveryTelemetryTagsConstant_IsAdmitted. A bare literal here would
        // compile, export nothing, and no test would notice.
        activity?.SetTag(TelemetryTags.RecoveryDataBoundary, result.TagValue);
        activity?.SetTag(TelemetryTags.RecoveryDataBoundaryElapsedMs, stopwatch.ElapsedMilliseconds);
        if (result.FailureReason is not null)
        {
            activity?.SetTag(TelemetryTags.RecoveryDataBoundaryFailureReason, result.FailureReason);
        }

        return result;
    }

    private async Task<DataBoundaryResult> ResolveCoreAsync(Activity? activity, string? presuppliedToken, CancellationToken ct)
    {
        // The budget starts here, not after the profile read: that read can block on file I/O or on
        // the profile service's own gate, and anything before the CTS exists is outside the ceiling
        // this lookup promises. A caller cancellation stays distinguishable from our timeout so
        // Ctrl+C is never reported as a lookup failure.
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(LookupTimeout);

        string? tenantId;
        try
        {
            var profile = await _profileService.GetActiveProfileAsync(timeoutCts.Token).ConfigureAwait(false);
            tenantId = profile?.TenantId;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return DataBoundaryResult.Failed("timeout_reading_profile");
        }
        catch (Exception ex)
        {
            return DataBoundaryResult.Failed($"profile_unavailable: {ex.GetType().Name}");
        }

        // Signed out, or a profile with no tenant. Skip the call rather than issue one that is
        // certain to fail.
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return DataBoundaryResult.Failed("no_active_tenant");
        }

        string? token;
        if (!string.IsNullOrEmpty(presuppliedToken))
        {
            // Caller already holds an ARM-audience token; acquiring a second one would duplicate
            // the whole MSAL flow for no gain.
            token = presuppliedToken;
        }
        else
        {
            // Timed separately from the ARM call: this is the expensive half when it runs at all,
            // and on the normal path it does not run, so folding it into one number would make the
            // lookup look costlier than it is.
            var tokenStopwatch = Stopwatch.StartNew();
            try
            {
                var auth = await _authService.AcquireTokenSilentlyAsync(
                    scopes: ArmScope,
                    tenantId: tenantId,
                    promptOnError: false,
                    cancellationToken: timeoutCts.Token).ConfigureAwait(false);
                token = auth?.AccessToken;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                return DataBoundaryResult.Failed("timeout_acquiring_token", tenantId);
            }
            catch (Exception ex)
            {
                return DataBoundaryResult.Failed($"token_unavailable: {ex.GetType().Name}", tenantId);
            }
            finally
            {
                // In finally so a failed or timed-out acquisition is measured too — those are the
                // slow ones, and dropping them would flatter the numbers.
                tokenStopwatch.Stop();
                activity?.SetTag(TelemetryTags.RecoveryDataBoundaryTokenElapsedMs, tokenStopwatch.ElapsedMilliseconds);
            }
        }

        if (string.IsNullOrEmpty(token))
        {
            return DataBoundaryResult.Failed("token_unavailable", tenantId);
        }

        var armStopwatch = Stopwatch.StartNew();
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get, $"{ArmEndpoint}{DataBoundaryPath}?api-version={ApiVersion}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // TODO (deferred): no retry while this is observation-only. A failed lookup just
            // records Unknown, so one attempt under LookupTimeout is enough and keeps the
            // recovery startup path short. This MUST change before the boundary selects an
            // endpoint: at that point every transient ARM blip — 429 throttling above all —
            // becomes a routing decision made on Unknown, and Unknown must never resolve to
            // Global. The portal's equivalent (DataBoundaryDataProvider) retries 3 times with
            // exponential backoff over 5xx, 408, 429, network errors and timeouts.
            using var response = await _http
                .SendAsync(request, HttpCompletionOption.ResponseContentRead, timeoutCts.Token)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                // The status alone is enough to triage; the body can name the scope, so it stays out.
                return DataBoundaryResult.Failed($"arm_status_{(int)response.StatusCode}", tenantId);
            }

            var body = await response.Content.ReadAsStringAsync(timeoutCts.Token).ConfigureAwait(false);
            return ParseResponse(body, tenantId);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return DataBoundaryResult.Failed("timeout", tenantId);
        }
        catch (Exception ex)
        {
            return DataBoundaryResult.Failed($"arm_request_failed: {ex.GetType().Name}", tenantId);
        }
        finally
        {
            // Separate from the caller's total so the ARM round trip is visible on its own.
            // Recorded on every path — a rejected or timed-out attempt still spends the user's
            // wall clock.
            armStopwatch.Stop();
            activity?.SetTag(TelemetryTags.RecoveryDataBoundaryArmElapsedMs, armStopwatch.ElapsedMilliseconds);
        }
    }

    private static DataBoundaryResult ParseResponse(string body, string tenantId)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return DataBoundaryResult.Failed("empty_response", tenantId);
        }

        string? rawValue;
        string? responseTenantId;
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            // TryGetProperty throws InvalidOperationException on anything that is not an object, so
            // a well-formed body of the wrong shape (an array, a bare number) has to be rejected by
            // kind first. Letting it throw would surface as arm_request_failed from the caller's
            // catch — blaming the transport for what is actually a response-shape problem.
            if (root.ValueKind != JsonValueKind.Object)
            {
                return DataBoundaryResult.Failed("malformed_response", tenantId);
            }

            // Same guard one level down: "properties" can legitimately be present as null or an
            // array, and both would throw rather than read as a missing boundary.
            rawValue = root.TryGetProperty("properties", out var properties)
                       && properties.ValueKind == JsonValueKind.Object
                       && properties.TryGetProperty("dataBoundary", out var dataBoundary)
                       && dataBoundary.ValueKind == JsonValueKind.String
                ? dataBoundary.GetString()
                : null;

            // ARM echoes the tenant id in 'name'. Comparing it catches a lookup that silently
            // resolved a different tenant than the profile we asked about.
            responseTenantId = root.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String
                ? name.GetString()
                : null;
        }
        catch (JsonException)
        {
            return DataBoundaryResult.Failed("malformed_response", tenantId);
        }

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return DataBoundaryResult.Failed("missing_data_boundary", tenantId);
        }

        // A missing echo is treated the same as a wrong one: the point of the check is to prove the
        // answer belongs to the tenant we asked about, and an absent name proves nothing. Accepting
        // it would let a trimmed or unexpected response pass as an authoritative "not EU".
        if (string.IsNullOrWhiteSpace(responseTenantId)
            || !string.Equals(responseTenantId, tenantId, StringComparison.OrdinalIgnoreCase))
        {
            return DataBoundaryResult.Failed("tenant_mismatch", tenantId, rawValue);
        }

        var parsed = DataBoundaryResult.Parse(rawValue);
        return parsed == DataBoundary.Unknown
            ? DataBoundaryResult.Failed("unrecognized_value", tenantId, rawValue)
            : DataBoundaryResult.Resolved(parsed, rawValue, tenantId);
    }
}
