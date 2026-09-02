// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using Microsoft.Azure.Azclips.Extensions.Storage.Models;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors;

public sealed class ListStorageBlobProcessor : StorageBlobProcessorBase<object, ListBlobsResponse>
{
    public string Url { get; set; } = string.Empty;
    public string Version { get; set; } = "2026-04-06";
    public Guid? ClientRequestId { get; set; }
    public string? Prefix { get; set; }
    public string? Marker { get; set; }
    public int? MaxResults { get; set; }
    public string[] Include { get; set; } = [];
    public int? Timeout { get; set; }
    public string? StartFrom { get; set; }

    protected override HttpMethod Method => HttpMethod.Get;

    protected override string CreateRequestUri()
    {
        List<string> query = ["restype=container", "comp=list"];
        if (!string.IsNullOrWhiteSpace(Prefix))
        {
            query.Add($"prefix={Uri.EscapeDataString(Prefix)}");
        }
        if (!string.IsNullOrWhiteSpace(Marker))
        {
            query.Add($"marker={Uri.EscapeDataString(Marker)}");
        }
        if (MaxResults is not null)
        {
            query.Add($"maxresults={MaxResults.Value.ToString(CultureInfo.InvariantCulture)}");
        }
        if (Include.Length > 0)
        {
            query.Add($"include={Uri.EscapeDataString(string.Join(",", Include))}");
        }
        if (Timeout is not null)
        {
            query.Add($"timeout={Timeout.Value.ToString(CultureInfo.InvariantCulture)}");
        }
        if (!string.IsNullOrWhiteSpace(StartFrom))
        {
            query.Add($"startFrom={Uri.EscapeDataString(StartFrom)}");
        }
        return $"{Url}{(Url.Contains('?', StringComparison.Ordinal) ? '&' : '?')}{string.Join('&', query)}";
    }

    protected override void CreateRequestHeaders(HttpRequestMessage request)
    {
        base.CreateRequestHeaders(request);
        request.Headers.TryAddWithoutValidation("x-ms-version", Version);
        if (ClientRequestId is not null)
        {
            request.Headers.TryAddWithoutValidation("x-ms-client-request-id", ClientRequestId.Value.ToString());
        }
    }
}
