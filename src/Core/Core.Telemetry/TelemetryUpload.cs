// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions;

namespace Core.Telemetry;

/// <summary>
/// Entry point for the detached <c>--upload-telemetry</c> process. Runs before the DI
/// container exists, so it wires the uploader by hand.
/// </summary>
public static class TelemetryUpload
{
    /// <summary>
    /// Sends all pending trace files, then deletes the ones that were delivered.
    /// An optional <paramref name="accessTokenProvider"/> can supply a cached ARM token
    /// so the data-boundary cache can be populated transparently in the background.
    /// </summary>
    public static void RunPending(Func<string?>? accessTokenProvider = null)
    {
        // Ensure the tenant→data-boundary cache is populated before building
        // exporters so the Aria exporter picks the correct regional endpoint.
        EnsureDataBoundaryCached(accessTokenProvider);

        new TraceUploader(
            new TraceFileStore(HomePaths.GetDefaultTraceDirectory()),
            new TracerProviderExportSink(TelemetryTargetResolver.Resolve()))
        .Run();
    }

    private static void EnsureDataBoundaryCached(Func<string?>? accessTokenProvider)
    {
        try
        {
            var tenantId = DataBoundaryResolver.GetActiveTenantId();
            if (tenantId is null)
                return;

            // Already cached — nothing to do.
            if (DataBoundaryCache.Get(tenantId) is not null)
                return;

            // No token provider — fall back to Global (safe default).
            var token = accessTokenProvider?.Invoke();
            if (string.IsNullOrEmpty(token))
                return;

            // Guard: ensure the token's tenant matches the active profile tenant
            // to avoid caching the wrong boundary for a mismatched account.
            if (!TokenMatchesTenant(token, tenantId))
                return;

            var boundary = DataBoundaryService
                .GetDataBoundaryAsync(token, CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            if (!string.IsNullOrEmpty(boundary))
                DataBoundaryCache.Set(tenantId, boundary);
        }
        catch
        {
            // Best effort — any failure falls back to Global.
        }
    }

    /// <summary>
    /// Returns <c>true</c> when the JWT's <c>tid</c> claim matches <paramref name="tenantId"/>.
    /// Returns <c>false</c> on any decode failure so the caller safely falls back to Global.
    /// </summary>
    private static bool TokenMatchesTenant(string token, string tenantId)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length < 2)
                return false;

            // JWT payload is base64url-encoded; pad to a multiple of 4 for Convert.
            var payload = parts[1];
            payload = payload.Replace('-', '+').Replace('_', '/');
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }

            var json = Convert.FromBase64String(payload);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("tid", out var tidElement))
                return string.Equals(tidElement.GetString(), tenantId, StringComparison.OrdinalIgnoreCase);

            return false;
        }
        catch
        {
            return false;
        }
    }
}
