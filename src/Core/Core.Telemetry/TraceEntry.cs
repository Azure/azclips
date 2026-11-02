// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Core.Telemetry;

/// <summary>
/// Deserialization model matching the JSON written by <see cref="FileExporter"/>.
/// </summary>
internal sealed class TraceEntry
{
    [JsonPropertyName("traceId")]
    public string? TraceId { get; set; }
    [JsonPropertyName("spanId")]
    public string? SpanId { get; set; }
    [JsonPropertyName("parentSpanId")]
    public string? ParentSpanId { get; set; }
    [JsonPropertyName("operationName")]
    public string? OperationName { get; set; }
    [JsonPropertyName("startTime")]
    public string? StartTime { get; set; }
    [JsonPropertyName("durationMs")]
    public double Duration { get; set; }
    [JsonPropertyName("status")]
    public string? Status { get; set; }
    [JsonPropertyName("kind")]
    public string? Kind { get; set; }
    [JsonPropertyName("tenantId")]
    public string? TenantId { get; set; }
    [JsonPropertyName("dataBoundary")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DataBoundary { get; set; }
    [JsonPropertyName("dataBoundaryOutcome")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DataBoundaryOutcome { get; set; }
    [JsonPropertyName("uploadRegion")]
    public string? UploadRegion { get; set; }
    [JsonPropertyName("entraObjectId")]
    public string? EntraObjectId { get; set; }
    [JsonPropertyName("tags")]
    public JsonElement Tags { get; set; }
    [JsonPropertyName("events")]
    public JsonElement Events { get; set; }
    [JsonPropertyName("resource")]
    public JsonElement Resource { get; set; }

    internal Core.Abstractions.UploadRegion GetUploadRegion() =>
        Core.Abstractions.UploadRegions.Resolve(
            UploadRegion ?? GetStringTag(Core.Abstractions.TelemetryTags.UploadRegion),
            DataBoundary ?? GetStringTag(Core.Abstractions.TelemetryTags.DataBoundary),
            DataBoundaryOutcome ?? GetStringTag(Core.Abstractions.TelemetryTags.DataBoundaryOutcome));

    internal string? GetStringTag(string name)
    {
        if (Tags.ValueKind != JsonValueKind.Array)
            return null;
        string? result = null;
        foreach (var tag in Tags.EnumerateArray())
        {
            if (tag.ValueKind != JsonValueKind.Object)
                return null;
            if (!tag.TryGetProperty("Key", out var key) || key.ValueKind != JsonValueKind.String ||
                key.GetString() != name)
                continue;
            if (result is not null || !tag.TryGetProperty("Value", out var value) ||
                value.ValueKind != JsonValueKind.String)
                return null;
            result = value.GetString();
        }
        return result;
    }

    /// <summary>Reads <c>service.name</c> from the Resource block, or null when absent.</summary>
    public string? TryGetServiceName()
    {
        if (Resource.ValueKind == JsonValueKind.Object &&
            Resource.TryGetProperty("service.name", out var nameElement) &&
            nameElement.ValueKind == JsonValueKind.String &&
            nameElement.GetString() is string name &&
            !string.IsNullOrEmpty(name))
        {
            return name;
        }

        return null;
    }
}
