// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;

namespace Core.Telemetry;

public static class TelemetryLayerTags
{
    public const string Schema = "schema";
    public const string ServiceName = "service.name";
    public const string InvocationId = "properties.recovery.invocationId";
    public const string Completion = "properties.telemetry.completion";
    public const string PendingProducers = "properties.telemetry.pendingProducers";
    public const string AbandonedSpans = "properties.telemetry.abandonedSpans";
}

public sealed record TelemetryInvocationContext(
    string InvocationId,
    bool Enabled,
    string? CommandTraceId = null,
    string? CommandSpanId = null,
    string? Command = null,
    string? TenantId = null,
    string? SubscriptionId = null,
    string? DataBoundary = null)
{
    public string EffectiveDataBoundary =>
        string.Equals(DataBoundary, "EU", StringComparison.OrdinalIgnoreCase) ? "EU" : "Global";

    public static TelemetryInvocationContext Capture(Activity? commandRoot, bool enabled, string? command = null) =>
        new(
            Guid.NewGuid().ToString("N"),
            enabled,
            commandRoot?.TraceId.ToString(),
            commandRoot?.SpanId.ToString(),
            commandRoot?.GetTagItem(TelemetryTags.Command) as string ?? command,
            commandRoot?.GetTagItem(TelemetryTags.TenantId) as string,
            commandRoot?.GetTagItem(TelemetryTags.SubscriptionId) as string,
            commandRoot?.GetTagItem(TelemetryTags.DataBoundary) as string);

    public static TelemetryInvocationContext StandaloneFromCache(bool enabled)
    {
        var tenant = DataBoundaryResolver.GetActiveTenantId();
        var boundary = tenant is null ? null : DataBoundaryCache.Get(tenant);
        return new TelemetryInvocationContext(Guid.NewGuid().ToString("N"), enabled,
            TenantId: tenant, DataBoundary: boundary);
    }
}

public enum TelemetryLayerCompletion
{
    Empty,
    Disabled,
    Published,
    PublishedAtDeadline,
    NotPublished,
    PublicationUnconfirmed,
}

public interface ITelemetryLayerScope : IAsyncDisposable
{
    ITelemetryService Telemetry { get; }
    TelemetryInvocationContext Context { get; }
    CancellationToken StoppingToken { get; }
    TimeSpan RemainingDrainTime { get; }

    IDisposable? TryEnterProducer(bool teardown = false);
    void BeginDrain();
    Task<bool> RunCleanupAsync(Func<Task> cleanup);
    Task<TelemetryLayerCompletion> CompleteAsync(
        Activity? root = null,
        IReadOnlyDictionary<string, object?>? terminalTags = null,
        ActivityStatusCode? terminalStatus = null);
}

public interface ITelemetryLayerScopeFactory
{
    ITelemetryLayerScope CreateScope(
        string layer,
        TelemetryInvocationContext context,
        string? fileId = null);
}
