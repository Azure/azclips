// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Core.Abstractions;

public static class TelemetryLayerEnvironment
{
    public const string Layer = "AZCLIPS_TELEMETRY_LAYER";
    public const string FileId = "AZCLIPS_TELEMETRY_LAYER_FILE_ID";
    public const string Context = "AZCLIPS_TELEMETRY_LAYER_CONTEXT";
    public const string Enabled = "AZCLIPS_TELEMETRY_LAYER_ENABLED";
    public const string Agent = "agent";

    public static void SetAgentLaunch(
        IDictionary<string, string?> environment,
        TelemetryInvocationContext context,
        string fileId)
    {
        if (!Guid.TryParseExact(fileId, "N", out _))
            throw new ArgumentException("A layer file ID must be a GUID in N format.", nameof(fileId));
        if (!Guid.TryParseExact(context.InvocationId, "N", out _))
            throw new ArgumentException("A layer invocation ID must be a GUID in N format.", nameof(context));

        environment.Remove("AZCLIPS_RECOVERY_TRACEPARENT");
        environment.Remove("AZCLIPS_RECOVERY_HIERARCHY");
        environment.Remove(Layer);
        environment.Remove(FileId);
        environment.Remove(Context);
        environment.Remove(Enabled);
        environment[Layer] = Agent;
        environment[FileId] = fileId;
        environment[Enabled] = context.Enabled ? "true" : "false";
        environment[Context] = JsonSerializer.Serialize(context, TelemetryEnvironmentJsonContext.Default.TelemetryInvocationContext);
    }

    public static bool ParentAllowsTelemetry(Func<string, string?> read) =>
        read(Enabled) is null or "" or "true";

    public static bool TryReadAgentLaunch(
        Func<string, string?> read,
        out TelemetryInvocationContext? context,
        out string? fileId,
        out string? diagnostic)
    {
        context = null;
        fileId = null;
        diagnostic = null;
        var layer = read(Layer);
        var id = read(FileId);
        var json = read(Context);
        if (layer is null && id is null && json is null)
            return false;

        if (layer != Agent || !Guid.TryParseExact(id, "N", out _) ||
            string.IsNullOrWhiteSpace(json) || json.Length > 8192 ||
            read(Enabled) is not ("true" or "false"))
        {
            diagnostic = "invalid_agent_telemetry_launch";
            return false;
        }

        try
        {
            var value = JsonSerializer.Deserialize(json, TelemetryEnvironmentJsonContext.Default.TelemetryInvocationContext);
            if (value is null ||
                (value.BoundaryOutcome is { } outcome && !Enum.IsDefined(outcome)) ||
                !Guid.TryParseExact(value.InvocationId, "N", out _) ||
                !IsOptionalHexId(value.CommandTraceId, 32) ||
                !IsOptionalHexId(value.CommandSpanId, 16) ||
                !IsOptionalParent(value.CommandTraceParent, value.CommandTraceState) ||
                !IsOptionalParent(value.RecoveryTraceParent, value.RecoveryTraceState) ||
                !ParentMatchesCommandIds(value))
            {
                diagnostic = "invalid_agent_telemetry_context";
                return false;
            }

            context = value with { Enabled = value.Enabled && ParentAllowsTelemetry(read) };
            fileId = id;
            return true;
        }
        catch (JsonException)
        {
            diagnostic = "invalid_agent_telemetry_context";
            return false;
        }
    }

    private static bool IsOptionalHexId(string? value, int length) =>
        value is null ||
        (value.Length == length && value.Any(character => character != '0') &&
         value.All(char.IsAsciiHexDigit));

    private static bool IsOptionalParent(string? traceParent, string? traceState)
    {
        if (traceParent is null)
            return traceState is null;
        if (traceState is { Length: > 512 } ||
            traceState?.Any(character => character is < ' ' or > '~') == true)
            return false;
        return ActivityContext.TryParse(traceParent, traceState, out var parent) &&
            parent.TraceId != default &&
            parent.SpanId != default;
    }

    private static bool ParentMatchesCommandIds(TelemetryInvocationContext context)
    {
        if (context.CommandTraceParent is not null &&
            (!context.TryGetCommandParentContext(out var command) ||
             !command.TraceId.ToString().Equals(context.CommandTraceId, StringComparison.OrdinalIgnoreCase) ||
             !command.SpanId.ToString().Equals(context.CommandSpanId, StringComparison.OrdinalIgnoreCase)))
            return false;
        return context.RecoveryTraceParent is null || context.CommandTraceId is null ||
            (context.TryGetRecoveryParentContext(out var recovery) &&
             recovery.TraceId.ToString().Equals(context.CommandTraceId, StringComparison.OrdinalIgnoreCase));
    }
}

[JsonSerializable(typeof(TelemetryInvocationContext))]
internal sealed partial class TelemetryEnvironmentJsonContext : JsonSerializerContext
{
}
