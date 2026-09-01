// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Headers;
using Core.Abstractions.Runtime;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors;

public sealed class BlockBlobUploadProcessor : StorageBlobProcessorBase<Stream, object>
{
    public string Url { get; set; } = string.Empty;
    public string Version { get; set; } = "2026-04-06";
    public Guid? ClientRequestId { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = [];
    public int? Timeout { get; set; }
    public byte[]? TransactionalContentMd5 { get; set; }
    public long ContentLength { get; set; }
    public string? BlobContentType { get; set; }
    public string? BlobContentEncoding { get; set; }
    public string? BlobContentLanguage { get; set; }
    public byte[]? BlobContentMd5 { get; set; }
    public string? BlobCacheControl { get; set; }
    public string? LeaseId { get; set; }
    public string? BlobContentDisposition { get; set; }
    public string? EncryptionKey { get; set; }
    public string? EncryptionKeySha256 { get; set; }
    public string? EncryptionAlgorithm { get; set; }
    public string? EncryptionScope { get; set; }
    public string? Tier { get; set; }
    public DateTimeOffset? IfModifiedSince { get; set; }
    public DateTimeOffset? IfUnmodifiedSince { get; set; }
    public string? IfNoneMatch { get; set; }
    public string? IfMatch { get; set; }
    public string? IfTags { get; set; }
    public string? BlobTagsString { get; set; }
    public DateTimeOffset? ImmutabilityPolicyExpiry { get; set; }
    public string? ImmutabilityPolicyMode { get; set; }
    public bool? LegalHold { get; set; }
    public byte[]? TransactionalContentCrc64 { get; set; }
    public string? StructuredBodyType { get; set; }
    public long? StructuredContentLength { get; set; }
    public string BlobType { get; set; } = "BlockBlob";
    public Stream Body { get; set; } = Stream.Null;

    protected override HttpMethod Method => HttpMethod.Put;

    protected override string CreateRequestUri() =>
        Timeout is null
            ? Url
            : $"{Url}{(Url.Contains('?', StringComparison.Ordinal) ? '&' : '?')}timeout={Timeout.Value}";

    protected override Stream CreateRequestBody() => Body;

    protected override void CreateRequestHeaders(HttpRequestMessage request)
    {
        base.CreateRequestHeaders(request);
        request.Options.Set(RuntimeHttpRequestOptions.DisableRetry, true);
        request.Options.Set(RuntimeHttpRequestOptions.DisableBodyLogging, true);
        request.Headers.TryAddWithoutValidation("x-ms-version", Version);
        if (ClientRequestId is not null)
        {
            request.Headers.TryAddWithoutValidation("x-ms-client-request-id", ClientRequestId.Value.ToString());
        }
        foreach ((string name, string value) in Metadata)
        {
            request.Headers.TryAddWithoutValidation($"x-ms-meta-{name}", value);
        }
        if (TransactionalContentMd5 is not null)
        {
            request.Content!.Headers.ContentMD5 = TransactionalContentMd5;
        }
        request.Content!.Headers.ContentLength = ContentLength;
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        if (!string.IsNullOrWhiteSpace(BlobContentType))
        {
            request.Headers.TryAddWithoutValidation("x-ms-blob-content-type", BlobContentType);
        }
        if (!string.IsNullOrWhiteSpace(BlobContentEncoding))
        {
            request.Headers.TryAddWithoutValidation("x-ms-blob-content-encoding", BlobContentEncoding);
        }
        if (!string.IsNullOrWhiteSpace(BlobContentLanguage))
        {
            request.Headers.TryAddWithoutValidation("x-ms-blob-content-language", BlobContentLanguage);
        }
        if (BlobContentMd5 is not null)
        {
            request.Headers.TryAddWithoutValidation("x-ms-blob-content-md5", Convert.ToBase64String(BlobContentMd5));
        }
        if (!string.IsNullOrWhiteSpace(BlobCacheControl))
        {
            request.Headers.TryAddWithoutValidation("x-ms-blob-cache-control", BlobCacheControl);
        }
        if (!string.IsNullOrWhiteSpace(LeaseId))
        {
            request.Headers.TryAddWithoutValidation("x-ms-lease-id", LeaseId);
        }
        if (!string.IsNullOrWhiteSpace(BlobContentDisposition))
        {
            request.Headers.TryAddWithoutValidation("x-ms-blob-content-disposition", BlobContentDisposition);
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
        if (!string.IsNullOrWhiteSpace(EncryptionScope))
        {
            request.Headers.TryAddWithoutValidation("x-ms-encryption-scope", EncryptionScope);
        }
        if (!string.IsNullOrWhiteSpace(Tier))
        {
            request.Headers.TryAddWithoutValidation("x-ms-access-tier", Tier);
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
        if (!string.IsNullOrWhiteSpace(BlobTagsString))
        {
            request.Headers.TryAddWithoutValidation("x-ms-tags", BlobTagsString);
        }
        if (ImmutabilityPolicyExpiry is not null)
        {
            request.Headers.TryAddWithoutValidation(
                "x-ms-immutability-policy-until-date",
                ImmutabilityPolicyExpiry.Value.ToString("R"));
        }
        if (!string.IsNullOrWhiteSpace(ImmutabilityPolicyMode))
        {
            request.Headers.TryAddWithoutValidation("x-ms-immutability-policy-mode", ImmutabilityPolicyMode);
        }
        if (LegalHold is not null)
        {
            request.Headers.TryAddWithoutValidation("x-ms-legal-hold", LegalHold.Value ? "true" : "false");
        }
        if (TransactionalContentCrc64 is not null)
        {
            request.Headers.TryAddWithoutValidation(
                "x-ms-content-crc64",
                Convert.ToBase64String(TransactionalContentCrc64));
        }
        if (!string.IsNullOrWhiteSpace(StructuredBodyType))
        {
            request.Headers.TryAddWithoutValidation("x-ms-structured-body", StructuredBodyType);
        }
        if (StructuredContentLength is not null)
        {
            request.Headers.TryAddWithoutValidation(
                "x-ms-structured-content-length",
                StructuredContentLength.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        request.Headers.TryAddWithoutValidation("x-ms-blob-type", BlobType);
    }
}
