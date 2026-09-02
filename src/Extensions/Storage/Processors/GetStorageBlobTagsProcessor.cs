// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Storage.Models;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors;

public sealed class GetStorageBlobTagsProcessor : StorageBlobProcessorBase<object, BlobTags>
{
    public string Url { get; set; } = string.Empty;
    public string Version { get; set; } = "2026-04-06";
    public Guid? ClientRequestId { get; set; }
    public int? Timeout { get; set; }
    public string? Snapshot { get; set; }
    public string? VersionId { get; set; }
    public string? LeaseId { get; set; }
    public string? IfTags { get; set; }
    public DateTimeOffset? BlobIfModifiedSince { get; set; }
    public DateTimeOffset? BlobIfUnmodifiedSince { get; set; }
    public string? BlobIfMatch { get; set; }
    public string? BlobIfNoneMatch { get; set; }

    protected override HttpMethod Method => HttpMethod.Get;

    protected override string CreateRequestUri()
    {
        List<string> query = ["comp=tags"];
        if (Timeout is not null)
        {
            query.Add($"timeout={Timeout.Value}");
        }
        if (!string.IsNullOrWhiteSpace(Snapshot))
        {
            query.Add($"snapshot={Uri.EscapeDataString(Snapshot)}");
        }
        if (!string.IsNullOrWhiteSpace(VersionId))
        {
            query.Add($"versionid={Uri.EscapeDataString(VersionId)}");
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
        if (!string.IsNullOrWhiteSpace(LeaseId))
        {
            request.Headers.TryAddWithoutValidation("x-ms-lease-id", LeaseId);
        }
        if (!string.IsNullOrWhiteSpace(IfTags))
        {
            request.Headers.TryAddWithoutValidation("x-ms-if-tags", IfTags);
        }
        if (BlobIfModifiedSince is not null)
        {
            request.Headers.TryAddWithoutValidation("x-ms-blob-if-modified-since", BlobIfModifiedSince.Value.ToString("R"));
        }
        if (BlobIfUnmodifiedSince is not null)
        {
            request.Headers.TryAddWithoutValidation("x-ms-blob-if-unmodified-since", BlobIfUnmodifiedSince.Value.ToString("R"));
        }
        if (!string.IsNullOrWhiteSpace(BlobIfMatch))
        {
            request.Headers.TryAddWithoutValidation("x-ms-blob-if-match", BlobIfMatch);
        }
        if (!string.IsNullOrWhiteSpace(BlobIfNoneMatch))
        {
            request.Headers.TryAddWithoutValidation("x-ms-blob-if-none-match", BlobIfNoneMatch);
        }
    }
}
