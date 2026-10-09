using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using Core.Abstractions;

namespace Core.Agent.Providers;

/// <summary>Observes the active tenant's ARM data boundary for startup telemetry.</summary>
/// <remarks>
/// This diagnostic probe does not select the model endpoint.
/// Query ARM with the caller's existing token; do not acquire another token.
/// Do not infer the boundary from IP, resource location or token claims.
/// </remarks>
internal sealed class DataBoundaryResolver : IDataBoundaryResolver
{
    private const string ArmEndpoint = "https://management.azure.com";

    // Tenant scope takes no prefix, so the path starts at /providers. Verified against ARM:
    // 2024-08-01 is the documented GA version and returns
    // { "name": "<tenantId>", "properties": { "dataBoundary": "Global", ... } }.
    private const string ApiVersion = "2024-08-01";
    private const string DataBoundaryPath = "/providers/Microsoft.Resources/dataBoundaries/default";

    /// <summary>
    /// Ceiling for the whole lookup. Enforced with a linked token so it holds even when the
    /// injected client has a longer timeout, because this sits on the startup path of every
    /// recovery and must not be able to stall it.
    /// </summary>
    internal static readonly TimeSpan LookupTimeout = TimeSpan.FromSeconds(5);

    private readonly IProfileService _profileService;
    private readonly HttpClient _http;
    private readonly bool _ownsHttpClient;
    private readonly ITelemetryService? _telemetry;

    public DataBoundaryResolver(
        IProfileService profileService, HttpClient? http = null, ITelemetryService? telemetry = null)
    {
        _profileService = profileService;
        _ownsHttpClient = http is null;
        _http = http ?? new HttpClient { Timeout = LookupTimeout };
        _telemetry = telemetry;
    }

    /// <summary>Disposes the owned client, but never an injected client.</summary>
    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _http.Dispose();
        }
    }

    /// <summary>Queries the active tenant's boundary with an existing token.</summary>
    /// <remarks>
    /// Caller cancellation propagates; other failures return a classified result.
    /// A failed diagnostic lookup must not prevent recovery.
    /// </remarks>
    public async Task<DataBoundaryResult> ResolveAsync(string accessToken, CancellationToken ct = default)
    {
        using var activity = _telemetry?.StartActivity("Agent.DataBoundary");
        var stopwatch = Stopwatch.StartNew();
        // Seeded rather than left unassigned because the finally below is also reached on the one
        // path that produces no result — the caller's own cancellation, rethrown — and a span
        // carrying no value is indistinguishable from one dropped at export.
        var result = DataBoundaryResult.NoAnswer(DataBoundaryUnavailable.Cancelled);
        try
        {
            result = await ResolveCoreAsync(accessToken, ct).ConfigureAwait(false);
            return result;
        }
        finally
        {
            stopwatch.Stop();
            // Tag keys come from TelemetryTags so they ride the vetted properties.* channel and are
            // covered by IsAllowed_EveryTelemetryTagsConstant_IsAdmitted. A bare literal here would
            // compile, export nothing, and no test would notice.
            activity?.SetTag(TelemetryTags.RecoveryDataBoundary, result.Value);
            activity?.SetTag(TelemetryTags.RecoveryDataBoundaryElapsedMs, stopwatch.ElapsedMilliseconds);
        }
    }

    private async Task<DataBoundaryResult> ResolveCoreAsync(string accessToken, CancellationToken ct)
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
            return DataBoundaryResult.NoAnswer(DataBoundaryUnavailable.TimeoutProfile);
        }
        catch (Exception)
        {
            // Distinct from no_tenant on purpose: being signed out is fixed by az login, while a
            // profile that cannot be read at all is a storage or permissions problem. Both end the
            // lookup here, but they send an operator to different places.
            return DataBoundaryResult.NoAnswer(DataBoundaryUnavailable.ProfileUnavailable);
        }

        // Signed out, or a profile with no tenant. Skip the call rather than issue one that is
        // certain to fail.
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return DataBoundaryResult.NoAnswer(DataBoundaryUnavailable.NoTenant);
        }

        if (string.IsNullOrEmpty(accessToken))
        {
            return DataBoundaryResult.NoAnswer(DataBoundaryUnavailable.NoToken, tenantId);
        }

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get, $"{ArmEndpoint}{DataBoundaryPath}?api-version={ApiVersion}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            // One bounded attempt is sufficient while this probe is diagnostic-only.
            // TODO: Add bounded retries before using its result for endpoint selection.
            // A failed lookup must never be treated as a confirmed Global boundary.
            using var response = await _http.SendAsync(request, timeoutCts.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                // The status alone is enough to triage; the body can name the scope, so it stays out.
                return DataBoundaryResult.FromArmStatus((int)response.StatusCode, tenantId);
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
            return DataBoundaryResult.NoAnswer(DataBoundaryUnavailable.TimeoutArm, tenantId);
        }
        catch (Exception)
        {
            return DataBoundaryResult.NoAnswer(DataBoundaryUnavailable.ArmFailed, tenantId);
        }
    }

    private static DataBoundaryResult ParseResponse(string body, string tenantId)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return DataBoundaryResult.NoAnswer(DataBoundaryUnavailable.Unreadable, tenantId);
        }

        string? rawValue;
        string? responseTenantId;
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            // TryGetProperty throws InvalidOperationException on anything that is not an object, so
            // a well-formed body of the wrong shape (an array, a bare number) has to be rejected by
            // kind first. Letting it throw would surface as arm_failed from the caller's catch —
            // blaming the transport for what is actually a response-shape problem.
            if (root.ValueKind != JsonValueKind.Object)
            {
                return DataBoundaryResult.NoAnswer(DataBoundaryUnavailable.Unreadable, tenantId);
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
            return DataBoundaryResult.NoAnswer(DataBoundaryUnavailable.Unreadable, tenantId);
        }

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return DataBoundaryResult.NoAnswer(DataBoundaryUnavailable.Unreadable, tenantId);
        }

        // An absent echo and a wrong one are both disqualifying, but they are not the same event:
        // a wrong one means ARM answered about someone else, while a missing one means the answer
        // proves nothing about whose boundary it is. Reporting the second as the first would send
        // triage looking for a tenant mix-up that never happened.
        if (string.IsNullOrWhiteSpace(responseTenantId))
        {
            return DataBoundaryResult.NoAnswer(DataBoundaryUnavailable.TenantUnconfirmed, tenantId);
        }

        if (!string.Equals(responseTenantId, tenantId, StringComparison.OrdinalIgnoreCase))
        {
            return DataBoundaryResult.NoAnswer(DataBoundaryUnavailable.TenantMismatch, tenantId);
        }

        return DataBoundaryResult.FromArm(rawValue, tenantId);
    }
}
