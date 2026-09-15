// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.Json;

namespace Core.Abstractions;

/// <summary>Records source identity before child work, independently of exporter span IDs.</summary>
public static class TelemetryHierarchy
{
    public const string RecoveryOperationName = "CLI.Activity.AiRecovery";
    public const string RecoveryRootPath = "Agent";
    public const string RecoveryTraceparentEnvVar = "AZCLIPS_RECOVERY_TRACEPARENT";
    public const string RecoveryHierarchyEnvVar = "AZCLIPS_RECOVERY_HIERARCHY";

    private const string ContextKey = "Core.Abstractions.TelemetryHierarchy";

    /// <summary>Immutable, in-process proof that a span's lineage was recorded at its source.</summary>
    public sealed class Context
    {
        internal Context(ActivityContext activityContext, int depth, string path)
        {
            ActivityContext = activityContext;
            Depth = depth;
            Path = path;
        }

        public ActivityContext ActivityContext { get; }
        public int Depth { get; }
        public string Path { get; }
    }

    public static Context? Capture(Activity? activity) =>
        activity?.GetCustomProperty(ContextKey) as Context;

    /// <param name="parentContext">
    /// Null inherits the ambient parent; an explicit default context starts an independent root.
    /// A remote parent without source metadata keeps its W3C edge, but is not labelled version 1.
    /// </param>
    public static Activity? StartActivity(
        ActivitySource source,
        string name,
        ActivityKind kind = ActivityKind.Internal,
        ActivityContext? parentContext = null,
        Context? parentHierarchy = null,
        DateTimeOffset startTime = default)
    {
        var parent = parentContext.HasValue ? parentHierarchy : Capture(Activity.Current);
        Activity? activity;
        if (parentContext.HasValue && parentContext.Value == default)
        {
            // Empty parentId opts out of ambient parenting. Set the format on this
            // activity, not process-global defaults (empty is not a hierarchical ID).
            activity = source.CreateActivity(name, kind, parentId: string.Empty, idFormat: ActivityIdFormat.W3C);
            if (startTime != default)
                activity?.SetStartTime(startTime.UtcDateTime);
            activity?.Start();
        }
        else
        {
            activity = source.StartActivity(name, kind, parentContext: parentContext ?? default, startTime: startTime);
        }
        if (activity is not null)
            Record(activity, parent);
        return activity;
    }

    /// <summary>
    /// Called only by source producers, never by buffer readers or replay. Unknown ancestors
    /// are not reconstructed from tags, names, timestamps, or an unrelated ambient activity.
    /// </summary>
    public static void Record(Activity activity, Context? parent = null)
    {
        if (activity.IdFormat != ActivityIdFormat.W3C || Capture(activity) is not null)
            return;

        parent ??= Capture(activity.Parent);
        int depth;
        string path;
        if (activity.ParentSpanId == default)
        {
            depth = 0;
            path = CanonicalSegment(activity.OperationName, isRoot: true);
        }
        else if (parent is not null
                 && parent.ActivityContext.TraceId == activity.TraceId
                 && parent.ActivityContext.SpanId == activity.ParentSpanId)
        {
            depth = checked(parent.Depth + 1);
            path = $"{parent.Path}.{CanonicalSegment(activity.OperationName, isRoot: false)}";
        }
        else
        {
            return;
        }

        activity.SetTag(TelemetryTags.HierarchyVersion, 1);
        activity.SetTag(TelemetryTags.HierarchySpanId, activity.SpanId.ToHexString());
        activity.SetTag(TelemetryTags.HierarchyParentSpanId,
            activity.ParentSpanId == default ? string.Empty : activity.ParentSpanId.ToHexString());
        activity.SetTag(TelemetryTags.HierarchyDepth, depth);
        activity.SetTag(TelemetryTags.HierarchyPath, path);
        activity.SetCustomProperty(ContextKey, new Context(activity.Context, depth, path));
    }

    // Dotted names (including Tool.<operation>) describe one span, not extra parents.
    private static string CanonicalSegment(string operationName, bool isRoot) => operationName switch
    {
        RecoveryOperationName => RecoveryRootPath,
        "Agent.Session" or "Agent.Create" or "Agent.Turn" or "Agent.Send" or "Agent.Reprompt" when !isRoot =>
            operationName["Agent.".Length..],
        _ when operationName.StartsWith("tool.", StringComparison.Ordinal) =>
            (isRoot ? "Agent.Tool." : "Tool.") + operationName["tool.".Length..],
        _ => operationName,
    };

    public static string? CreateRecoveryHandoff(Activity? recovery)
    {
        var context = Capture(recovery);
        if (context is null)
            return null;
        if (recovery?.OperationName != RecoveryOperationName
            || context.Depth != 0 || context.Path != RecoveryRootPath)
            throw new ArgumentException("Only a source-recorded recovery root can be handed off.", nameof(recovery));

        return JsonSerializer.Serialize(new RecoveryHandoff
        {
            Version = 1,
            TraceId = context.ActivityContext.TraceId.ToHexString(),
            SpanId = context.ActivityContext.SpanId.ToHexString(),
            Depth = context.Depth,
            Path = context.Path,
        });
    }

    public static bool TryReadRecoveryHandoff(
        ActivityContext traceparent, string? value, out Context? parent)
    {
        parent = null;
        if (traceparent == default || string.IsNullOrEmpty(value) || value.Length > 1024)
            return false;

        RecoveryHandoff? handoff;
        try
        {
            handoff = JsonSerializer.Deserialize<RecoveryHandoff>(value);
        }
        catch (JsonException)
        {
            return false;
        }

        // This protocol hands off only the recovery root, not an arbitrary user-supplied
        // path. Bind it to the W3C identity so inherited/stale metadata cannot reparent us.
        if (handoff is not { Version: 1, Depth: 0, Path: RecoveryRootPath }
            || handoff.TraceId != traceparent.TraceId.ToHexString()
            || handoff.SpanId != traceparent.SpanId.ToHexString())
            return false;

        parent = new Context(traceparent, handoff.Depth.Value, handoff.Path);
        return true;
    }

    private sealed class RecoveryHandoff
    {
        public int? Version { get; init; }
        public string? TraceId { get; init; }
        public string? SpanId { get; init; }
        public int? Depth { get; init; }
        public string? Path { get; init; }
    }
}
