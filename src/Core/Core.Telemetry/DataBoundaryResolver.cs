// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions;

namespace Core.Telemetry;

/// <summary>
/// Resolves confirmed tenant boundaries or unavailable lookup outcomes, using the cache first.
/// </summary>
public static class DataBoundaryResolver
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Boundary-only compatibility API for callers such as Agent model routing.
    /// Returns null for unavailable outcomes; telemetry callers use <see cref="ResolveUploadRegionAsync"/>.
    /// </summary>
    public static async Task<string?> ResolveAndCacheAsync(
        string? tenantId,
        Func<CancellationToken, Task<string?>> accessTokenProvider,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var resolution = await ResolveAsync(
            tenantId, accessTokenProvider, timeout, cancellationToken).ConfigureAwait(false);
        return resolution.Boundary;
    }

    public static Task<UploadRegion> ResolveUploadRegionAsync(
        string? tenantId,
        Func<CancellationToken, Task<string?>> accessTokenProvider,
        TimeSpan timeout,
        CancellationToken cancellationToken = default) =>
        ResolveUploadRegionAsync(tenantId, accessTokenProvider, DataBoundaryService.GetDataBoundaryAsync,
            timeout, cancellationToken);

    internal static async Task<UploadRegion> ResolveUploadRegionAsync(
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

    internal static Task<DataBoundaryResolution> ResolveAsync(
        string? tenantId,
        Func<CancellationToken, Task<string?>> accessTokenProvider,
        TimeSpan timeout,
        CancellationToken cancellationToken = default) =>
        ResolveAsync(tenantId, accessTokenProvider, DataBoundaryService.GetDataBoundaryAsync, timeout, cancellationToken);

    internal static async Task<DataBoundaryResolution> ResolveAsync(
        string? tenantId,
        Func<CancellationToken, Task<string?>> accessTokenProvider,
        Func<string, CancellationToken, Task<string?>> dataBoundaryProvider,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(tenantId))
            return new(null, DataBoundaryOutcome.TenantUnavailable);

        var cached = DataBoundaryCache.Get(tenantId);
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
            DataBoundaryCache.Set(tenantId, normalized);
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

    private static void ReportFailure(DataBoundaryOutcome outcome, string reason) =>
        Console.Error.WriteLine($"[{nameof(DataBoundaryResolver)}] {outcome}: {reason}.");

    internal static (string? TenantId, UploadRegion UploadRegion) ResolveCachedUploadRegion()
    {
        var tenant = GetActiveTenantId();
        var boundary = string.IsNullOrWhiteSpace(tenant) ? null : DataBoundaryCache.Get(tenant);
        var confirmed = DataBoundaryRouting.IsConfirmed(boundary);
        var outcome = confirmed ? DataBoundaryOutcome.Confirmed :
            string.IsNullOrWhiteSpace(tenant) ? DataBoundaryOutcome.TenantUnavailable :
            DataBoundaryOutcome.BoundaryUnavailable;
        return (tenant, DataBoundaryRoutingPolicy.Default.Resolve(confirmed ? boundary : null, outcome));
    }

    /// <summary>
    /// Returns the tenant ID from the active profile, or <c>null</c> if unavailable.
    /// </summary>
    internal static string? GetActiveTenantId()
    {
        try
        {
            var root = HomePaths.GetDefaultHomeRoot();

            var activeProfilePath = Path.Combine(root, HomePaths.ActiveProfileFileName);
            if (!File.Exists(activeProfilePath))
                return null;

            var activeProfileName = File.ReadAllText(activeProfilePath).Trim();
            if (string.IsNullOrEmpty(activeProfileName))
                return null;

            var profilesPath = Path.Combine(root, HomePaths.ProfilesFileName);
            if (!File.Exists(profilesPath))
                return null;

            var json = File.ReadAllText(profilesPath);
            var profiles = JsonSerializer.Deserialize<Dictionary<string, ProfileEntry>>(json, JsonOptions);

            if (profiles is not null && profiles.TryGetValue(activeProfileName, out var profile))
                return profile.TenantId;
        }
        catch
        {
            // Best effort.
        }

        return null;
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

    private sealed class ProfileEntry
    {
        public string? TenantId { get; set; }
    }
}
