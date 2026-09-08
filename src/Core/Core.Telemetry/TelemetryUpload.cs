// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
}
