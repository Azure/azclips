// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors;

public sealed class BlobDownloadProcessor : StorageBlobProcessorBase<object, Stream>
{
    public string Url { get; set; } = string.Empty;
    public string Version { get; set; } = "2026-04-06";
    public Guid? ClientRequestId { get; set; }
    public string? Snapshot { get; set; }
    public string? VersionId { get; set; }
    public int? Timeout { get; set; }
    public string? Range { get; set; }
    public string? LeaseId { get; set; }
    public bool? RangeGetContentMd5 { get; set; }
    public bool? RangeGetContentCrc64 { get; set; }
    public string? StructuredBodyType { get; set; }
    public string? EncryptionKey { get; set; }
    public string? EncryptionKeySha256 { get; set; }
    public string? EncryptionAlgorithm { get; set; }
    public string? IfTags { get; set; }
    public DateTimeOffset? IfModifiedSince { get; set; }
    public DateTimeOffset? IfUnmodifiedSince { get; set; }
    public string? IfNoneMatch { get; set; }
    public string? IfMatch { get; set; }

    protected override HttpMethod Method => HttpMethod.Get;

    protected override string CreateRequestUri()
    {
        List<string> query = [];
        if (!string.IsNullOrWhiteSpace(Snapshot))
        {
            query.Add($"snapshot={Uri.EscapeDataString(Snapshot)}");
        }
        if (!string.IsNullOrWhiteSpace(VersionId))
        {
            query.Add($"versionid={Uri.EscapeDataString(VersionId)}");
        }
        if (Timeout is not null)
        {
            query.Add($"timeout={Timeout.Value}");
        }
        return query.Count == 0
            ? Url
            : $"{Url}{(Url.Contains('?', StringComparison.Ordinal) ? '&' : '?')}{string.Join('&', query)}";
    }

    protected override void CreateRequestHeaders(HttpRequestMessage request)
    {
        base.CreateRequestHeaders(request);
        request.Options.Set(RuntimeHttpRequestOptions.DisableBodyLogging, true);
        request.Headers.TryAddWithoutValidation("x-ms-version", Version);
        if (ClientRequestId is not null)
        {
            request.Headers.TryAddWithoutValidation("x-ms-client-request-id", ClientRequestId.Value.ToString());
        }
        if (!string.IsNullOrWhiteSpace(Range))
        {
            request.Headers.TryAddWithoutValidation("Range", Range);
        }
        if (!string.IsNullOrWhiteSpace(LeaseId))
        {
            request.Headers.TryAddWithoutValidation("x-ms-lease-id", LeaseId);
        }
        if (RangeGetContentMd5 is not null)
        {
            request.Headers.TryAddWithoutValidation(
                "x-ms-range-get-content-md5",
                RangeGetContentMd5.Value ? "true" : "false");
        }
        if (RangeGetContentCrc64 is not null)
        {
            request.Headers.TryAddWithoutValidation(
                "x-ms-range-get-content-crc64",
                RangeGetContentCrc64.Value ? "true" : "false");
        }
        if (!string.IsNullOrWhiteSpace(StructuredBodyType))
        {
            request.Headers.TryAddWithoutValidation("x-ms-structured-body", StructuredBodyType);
        }
        if (!string.IsNullOrWhiteSpace(EncryptionKey))
        {
            request.Headers.TryAddWithoutValidation("x-ms-encryption-key", EncryptionKey);
        }
        if (!string.IsNullOrWhiteSpace(EncryptionKeySha256))
        {
            request.Headers.TryAddWithoutValidation("x-ms-encryption-key-sha256", EncryptionKeySha256);
        }
        if (!string.IsNullOrWhiteSpace(EncryptionAlgorithm))
        {
            request.Headers.TryAddWithoutValidation("x-ms-encryption-algorithm", EncryptionAlgorithm);
        }
        if (!string.IsNullOrWhiteSpace(IfTags))
        {
            request.Headers.TryAddWithoutValidation("x-ms-if-tags", IfTags);
        }
        if (IfModifiedSince is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Modified-Since", IfModifiedSince.Value.ToString("R"));
        }
        if (IfUnmodifiedSince is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Unmodified-Since", IfUnmodifiedSince.Value.ToString("R"));
        }
        if (!string.IsNullOrWhiteSpace(IfNoneMatch))
        {
            request.Headers.TryAddWithoutValidation("If-None-Match", IfNoneMatch);
        }
        if (!string.IsNullOrWhiteSpace(IfMatch))
        {
            request.Headers.TryAddWithoutValidation("If-Match", IfMatch);
        }
    }
}
