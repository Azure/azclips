// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Core.Telemetry;

public static class TelemetryLayerEnvironment
{
    public const string Layer = "AZCLIPS_TELEMETRY_LAYER";
    public const string FileId = "AZCLIPS_TELEMETRY_LAYER_FILE_ID";
    public const string Context = "AZCLIPS_TELEMETRY_LAYER_CONTEXT";
    public const string Enabled = "AZCLIPS_TELEMETRY_LAYER_ENABLED";
    public const string Agent = "agent";

    public static void SetAgentLaunch(
        IDictionary<string, string?> environment,
        TelemetryInvocationContext context)
    {
        if (!Guid.TryParseExact(context.InvocationId, "N", out _))
            throw new ArgumentException("A layer invocation ID must be a GUID in N format.", nameof(context));

        environment.Remove("AZCLIPS_RECOVERY_TRACEPARENT");
        environment.Remove("AZCLIPS_RECOVERY_HIERARCHY");
        environment.Remove(Layer);
        environment.Remove(FileId);
        environment.Remove(Context);
        environment.Remove(Enabled);
        environment[Layer] = Agent;
        environment[Enabled] = context.Enabled ? "true" : "false";
        environment[Context] = JsonSerializer.Serialize(context, TelemetryEnvironmentJsonContext.Default.TelemetryInvocationContext);
    }

    public static bool ParentAllowsTelemetry(Func<string, string?> read) =>
        read(Enabled) is null or "" or "true";

    public static bool TryReadAgentLaunch(
        Func<string, string?> read,
        out TelemetryInvocationContext? context,
        out string? diagnostic)
    {
        context = null;
        diagnostic = null;
        var layer = read(Layer);
        var id = read(FileId);
        var json = read(Context);
        if (layer is null && id is null && json is null)
            return false;

        if (layer != Agent || id is not null ||
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
                !Guid.TryParseExact(value.InvocationId, "N", out _) ||
                !IsOptionalHexId(value.CommandTraceId, 32) ||
                !IsOptionalHexId(value.CommandSpanId, 16))
            {
                diagnostic = "invalid_agent_telemetry_context";
                return false;
            }
            context = value with { Enabled = value.Enabled && ParentAllowsTelemetry(read) };
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
}

[JsonSerializable(typeof(TelemetryInvocationContext))]
internal sealed partial class TelemetryEnvironmentJsonContext : JsonSerializerContext
{
}
