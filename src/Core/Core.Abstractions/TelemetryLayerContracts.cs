// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;

namespace Core.Abstractions;

public static class TelemetryLayerTags
{
    public const string Schema = "schema";
    public const string ServiceName = "service.name";
    public const string InvocationId = "properties.recovery.invocationId";
}

public sealed record TelemetryInvocationContext(
    string InvocationId,
    bool Enabled,
    string? CommandTraceId = null,
    string? CommandSpanId = null,
    string? Command = null,
    string? TenantId = null,
    string? SubscriptionId = null,
    string? DataBoundary = null,
    string? CommandTraceParent = null,
    string? CommandTraceState = null,
    string? RecoveryTraceParent = null,
    string? RecoveryTraceState = null,
    DataBoundaryOutcome? BoundaryOutcome = null)
{
    public string EffectiveDataBoundary => NormalizeDataBoundary(DataBoundary);

    public static string NormalizeDataBoundary(string? dataBoundary)
    {
        if (string.IsNullOrWhiteSpace(dataBoundary))
            return "Unknown";
        if (string.Equals(dataBoundary, "EU", StringComparison.OrdinalIgnoreCase))
            return "EU";
        if (string.Equals(dataBoundary, "Global", StringComparison.OrdinalIgnoreCase))
            return "Global";
        return dataBoundary;
    }

    public bool TryGetCommandParentContext(out ActivityContext parentContext) =>
        TryGetParentContext(CommandTraceParent, CommandTraceState, false, out parentContext);

    public bool TryGetRecoveryParentContext(out ActivityContext parentContext) =>
        TryGetParentContext(RecoveryTraceParent, RecoveryTraceState, true, out parentContext);

    public TelemetryInvocationContext WithRecoveryParent(Activity? activity) =>
        this with
        {
            RecoveryTraceParent = activity is null ? null : FormatTraceParent(activity),
            RecoveryTraceState = activity?.TraceStateString,
        };

    public static TelemetryInvocationContext Capture(Activity? commandRoot, bool enabled, string? command = null) =>
        new(
            Guid.NewGuid().ToString("N"),
            enabled,
            commandRoot?.TraceId.ToString(),
            commandRoot?.SpanId.ToString(),
            commandRoot?.GetTagItem(TelemetryTags.CommandName) as string ?? command,
            commandRoot?.GetTagItem(TelemetryTags.TenantId) as string,
            commandRoot?.GetTagItem(TelemetryTags.SubscriptionId) as string,
            commandRoot?.GetTagItem(TelemetryTags.DataBoundary) as string,
            commandRoot is null ? null : FormatTraceParent(commandRoot),
            commandRoot?.TraceStateString,
            BoundaryOutcome: ParseBoundaryOutcome(commandRoot?.GetTagItem(TelemetryTags.DataBoundaryOutcome) as string));

    private static DataBoundaryOutcome? ParseBoundaryOutcome(string? value) =>
        Enum.TryParse<DataBoundaryOutcome>(value, ignoreCase: true, out var outcome) &&
        Enum.IsDefined(outcome) && string.Equals(value, outcome.ToString(), StringComparison.OrdinalIgnoreCase)
            ? outcome
            : null;

    private static bool TryGetParentContext(
        string? traceParent,
        string? traceState,
        bool isRemote,
        out ActivityContext parentContext)
    {
        parentContext = default;
        return traceParent is not null &&
            ActivityContext.TryParse(traceParent, traceState, isRemote, out parentContext) &&
            parentContext.TraceId != default &&
            parentContext.SpanId != default;
    }

    private static string FormatTraceParent(Activity activity) =>
        $"00-{activity.TraceId}-{activity.SpanId}-{((byte)activity.ActivityTraceFlags & 1):x2}";
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

    /// <summary>The owner finishes its producers first, or disallows publication if they did not finish.</summary>
    Task<TelemetryLayerCompletion> CompleteAsync(
        Activity? root = null,
        IReadOnlyDictionary<string, object?>? terminalTags = null,
        ActivityStatusCode? terminalStatus = null,
        bool allowPublication = true);
}

public interface ITelemetryLayerScopeFactory
{
    ITelemetryLayerScope CreateScope(
        string layer,
        TelemetryInvocationContext context,
        string? fileId = null);
}

public interface ICommandTelemetryContext
{
    void BindCommand(Activity? root);
    TelemetryInvocationContext Capture(string? command = null);
}
