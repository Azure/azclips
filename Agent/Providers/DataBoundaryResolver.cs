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
    public async Task<DataBoundaryResult> ResolveAsync(CancellationToken ct = default)
    {
        using var activity = AgentTracing.Source.StartActivity("Agent.DataBoundary");
        var stopwatch = Stopwatch.StartNew();

        var result = await ResolveCoreAsync(ct).ConfigureAwait(false);

        stopwatch.Stop();
        activity?.SetTag("dataBoundary", result.TagValue);
        activity?.SetTag("elapsedMs", stopwatch.ElapsedMilliseconds);
        if (result.FailureReason is not null)
        {
            activity?.SetTag("failureReason", result.FailureReason);
        }

        return result;
    }

    private async Task<DataBoundaryResult> ResolveCoreAsync(CancellationToken ct)
    {
        string? tenantId;
        try
        {
            var profile = await _profileService.GetActiveProfileAsync(ct).ConfigureAwait(false);
            tenantId = profile?.TenantId;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
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

        // The timeout is ours, but a caller cancellation has to stay distinguishable from it so
        // Ctrl+C is not reported as a lookup failure.
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(LookupTimeout);

        string? token;
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

        if (string.IsNullOrEmpty(token))
        {
            return DataBoundaryResult.Failed("token_unavailable", tenantId);
        }

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get, $"{ArmEndpoint}{DataBoundaryPath}?api-version={ApiVersion}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

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

            rawValue = root.TryGetProperty("properties", out var properties)
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

        if (!string.IsNullOrWhiteSpace(responseTenantId)
            && !string.Equals(responseTenantId, tenantId, StringComparison.OrdinalIgnoreCase))
        {
            return DataBoundaryResult.Failed("tenant_mismatch", tenantId, rawValue);
        }

        var parsed = DataBoundaryResult.Parse(rawValue);
        return parsed == DataBoundary.Unknown
            ? DataBoundaryResult.Failed("unrecognized_value", tenantId, rawValue)
            : DataBoundaryResult.Resolved(parsed, rawValue, tenantId);
    }
}
