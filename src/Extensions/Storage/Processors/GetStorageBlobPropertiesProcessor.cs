// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors;

public sealed class GetStorageBlobPropertiesProcessor : StorageBlobProcessorBase<object, object>
{
    public string Url { get; set; } = string.Empty;
    public string Version { get; set; } = "2026-04-06";
    public Guid? ClientRequestId { get; set; }
    public string? Snapshot { get; set; }
    public string? VersionId { get; set; }
    public int? Timeout { get; set; }
    public string? LeaseId { get; set; }
    public string? EncryptionKey { get; set; }
    public string? EncryptionKeySha256 { get; set; }
    public string? EncryptionAlgorithm { get; set; }
    public DateTimeOffset? IfModifiedSince { get; set; }
    public DateTimeOffset? IfUnmodifiedSince { get; set; }
    public string? IfNoneMatch { get; set; }
    public string? IfMatch { get; set; }
    public string? IfTags { get; set; }

    protected override HttpMethod Method => HttpMethod.Head;

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
        request.Headers.TryAddWithoutValidation("x-ms-version", Version);
        if (ClientRequestId is not null)
        {
            request.Headers.TryAddWithoutValidation("x-ms-client-request-id", ClientRequestId.Value.ToString());
        }
        if (!string.IsNullOrWhiteSpace(LeaseId))
        {
            request.Headers.TryAddWithoutValidation("x-ms-lease-id", LeaseId);
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
        if (!string.IsNullOrWhiteSpace(IfTags))
        {
            request.Headers.TryAddWithoutValidation("x-ms-if-tags", IfTags);
        }
    }
}
