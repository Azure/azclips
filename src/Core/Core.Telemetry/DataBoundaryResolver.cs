// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions;

namespace Core.Telemetry;

/// <summary>
/// Resolves confirmed tenant boundaries or unavailable lookup outcomes, using the cache first.
/// </summary>
public sealed class DataBoundaryResolver(
    IPersistenceStore persistence,
    IProfileService profileService,
    ILoggingService? loggingService = null)
{
    private readonly DataBoundaryCache _cache = new(persistence);

    /// <summary>
    /// Boundary-only compatibility API for callers such as Agent model routing.
    /// Returns null for unavailable outcomes; telemetry callers use <see cref="ResolveUploadRegionAsync"/>.
    /// </summary>
    public async Task<string?> ResolveAndCacheAsync(
        string? tenantId,
        Func<CancellationToken, Task<string?>> accessTokenProvider,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var resolution = await ResolveAsync(
            tenantId, accessTokenProvider, timeout, cancellationToken).ConfigureAwait(false);
        return resolution.Boundary;
    }

    public Task<UploadRegion> ResolveUploadRegionAsync(
        string? tenantId,
        Func<CancellationToken, Task<string?>> accessTokenProvider,
        TimeSpan timeout,
        CancellationToken cancellationToken = default) =>
        ResolveUploadRegionAsync(tenantId, accessTokenProvider, DataBoundaryService.GetDataBoundaryAsync,
            timeout, cancellationToken);

    internal async Task<UploadRegion> ResolveUploadRegionAsync(
        string? tenantId,
        Func<CancellationToken, Task<string?>> accessTokenProvider,
        Func<string, CancellationToken, Task<string?>> dataBoundaryProvider,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var result = await ResolveAsync(tenantId, accessTokenProvider, dataBoundaryProvider,
            timeout, cancellationToken).ConfigureAwait(false);
        return DataBoundaryRoutingPolicy.Default.Resolve(result.Boundary, result.Outcome);
    }

    internal Task<DataBoundaryResolution> ResolveAsync(
        string? tenantId,
        Func<CancellationToken, Task<string?>> accessTokenProvider,
        TimeSpan timeout,
        CancellationToken cancellationToken = default) =>
        ResolveAsync(tenantId, accessTokenProvider, DataBoundaryService.GetDataBoundaryAsync, timeout, cancellationToken);

    internal async Task<DataBoundaryResolution> ResolveAsync(
        string? tenantId,
        Func<CancellationToken, Task<string?>> accessTokenProvider,
        Func<string, CancellationToken, Task<string?>> dataBoundaryProvider,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(tenantId))
            return new(null, DataBoundaryOutcome.TenantUnavailable);

        var cached = await _cache.GetAsync(tenantId, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (DataBoundaryRouting.IsConfirmed(cached))
            return new(DataBoundaryRouting.Normalize(cached), DataBoundaryOutcome.Confirmed);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        string? token;
        try
        {
            token = await accessTokenProvider(timeoutCts.Token).ConfigureAwait(false);
            timeoutCts.Token.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            ReportFailure(DataBoundaryOutcome.AuthenticationUnavailable, ex.GetType().Name);
            return new(null, DataBoundaryOutcome.AuthenticationUnavailable);
        }

        if (string.IsNullOrEmpty(token) || !TokenMatchesTenant(token, tenantId))
        {
            ReportFailure(DataBoundaryOutcome.AuthenticationUnavailable, "Missing or non-matching token");
            return new(null, DataBoundaryOutcome.AuthenticationUnavailable);
        }

        try
        {
            var boundary = await dataBoundaryProvider(token, timeoutCts.Token).ConfigureAwait(false);
            timeoutCts.Token.ThrowIfCancellationRequested();
            if (!DataBoundaryRouting.IsConfirmed(boundary))
            {
                ReportFailure(DataBoundaryOutcome.BoundaryUnavailable, "No confirmed boundary");
                return new(null, DataBoundaryOutcome.BoundaryUnavailable);
            }

            var normalized = DataBoundaryRouting.Normalize(boundary);
            await _cache.SetAsync(tenantId, normalized, cancellationToken).ConfigureAwait(false);
            return new(normalized, DataBoundaryOutcome.Confirmed);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            ReportFailure(DataBoundaryOutcome.BoundaryUnavailable, ex.GetType().Name);
            return new(null, DataBoundaryOutcome.BoundaryUnavailable);
        }
    }

    private void ReportFailure(DataBoundaryOutcome outcome, string reason) =>
        loggingService?.Verbose($"[{nameof(DataBoundaryResolver)}] {outcome}: {reason}.");

    internal async Task<(string? TenantId, UploadRegion UploadRegion)> ResolveCachedUploadRegionAsync()
    {
        string? tenant;
        try
        {
            tenant = (await profileService.GetActiveProfileAsync().ConfigureAwait(false))?.TenantId;
        }
        catch (Exception ex)
        {
            loggingService?.Verbose($"[{nameof(DataBoundaryResolver)}] Profile read failed: {ex.GetType().Name}.");
            tenant = null;
        }
        var boundary = string.IsNullOrWhiteSpace(tenant) ? null : await _cache.GetAsync(tenant).ConfigureAwait(false);
        var confirmed = DataBoundaryRouting.IsConfirmed(boundary);
        var outcome = confirmed ? DataBoundaryOutcome.Confirmed :
            string.IsNullOrWhiteSpace(tenant) ? DataBoundaryOutcome.TenantUnavailable :
            DataBoundaryOutcome.BoundaryUnavailable;
        return (tenant, DataBoundaryRoutingPolicy.Default.Resolve(confirmed ? boundary : null, outcome));
    }

    private static bool TokenMatchesTenant(string token, string tenantId)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length < 2)
                return false;

            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }

            var json = Convert.FromBase64String(payload);
            using var doc = JsonDocument.Parse(json);

            return doc.RootElement.TryGetProperty("tid", out var tidElement) &&
                   string.Equals(tidElement.GetString(), tenantId, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

}
