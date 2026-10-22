// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;

namespace Core.Telemetry;

/// <summary>
/// Deserialization model matching the JSON written by <see cref="FileExporter"/>.
/// </summary>
internal sealed class TraceEntry
{
    public string? TraceId { get; set; }
    public string? SpanId { get; set; }
    public string? ParentSpanId { get; set; }
    public string? OperationName { get; set; }
    public string? DisplayName { get; set; }
    public string? StartTime { get; set; }
    public double Duration { get; set; }
    public string? Status { get; set; }
    public string? StatusDescription { get; set; }
    public string? Kind { get; set; }
    public string? TenantId { get; set; }
    public string? DataBoundary { get; set; }
    public string? EntraObjectId { get; set; }
    public JsonElement Tags { get; set; }
    public JsonElement Events { get; set; }
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
