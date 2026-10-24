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
    [JsonPropertyName("statusDescription")]
    public string? StatusDescription { get; set; }
    [JsonPropertyName("kind")]
    public string? Kind { get; set; }
    [JsonPropertyName("tenantId")]
    public string? TenantId { get; set; }
    [JsonPropertyName("dataBoundary")]
    public string? DataBoundary { get; set; }
    [JsonPropertyName("entraObjectId")]
    public string? EntraObjectId { get; set; }
    [JsonPropertyName("tags")]
    public JsonElement Tags { get; set; }
    [JsonPropertyName("events")]
    public JsonElement Events { get; set; }
    [JsonPropertyName("resource")]
    public JsonElement Resource { get; set; }

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
