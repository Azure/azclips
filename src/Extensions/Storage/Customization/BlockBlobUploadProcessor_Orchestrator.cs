// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Storage.Models;
using System.Net;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors;

public sealed class BlockBlobUploadProcessor_Orchestrator : StorageBlobProcessorBase<Stream, object>
{
    private string _blobUrl = string.Empty;
    private string _sourcePath = string.Empty;
    private long _contentLength;
    private string? _etag;
    private string? _lastModified;

    public string AccountName { get; set; } = string.Empty;
    public string ContainerName { get; set; } = string.Empty;
    public string BlobName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public bool Overwrite { get; set; }
    public string[] Metadata { get; set; } = [];
    public string[] Tags { get; set; } = [];
    public string[] Properties { get; set; } = [];
    public string? StandardBlobTier { get; set; }
    public string? EncryptionScope { get; set; }
    public string? TagCondition { get; set; }

    internal override async Task<HttpResponseMessage> SendAsync()
    {
        if (string.IsNullOrWhiteSpace(FilePath))
        {
            throw new ArgumentException("FilePath is required.", nameof(FilePath));
        }

        _sourcePath = Path.GetFullPath(FilePath);
        var sourceInfo = new FileInfo(_sourcePath);
        if (!sourceInfo.Exists)
        {
            throw new FileNotFoundException($"Upload file '{_sourcePath}' does not exist.", _sourcePath);
        }
        if (string.IsNullOrWhiteSpace(BlobName))
        {
            BlobName = Path.GetFileName(_sourcePath);
        }
        if (ResolvedProfile is null)
        {
            throw new InvalidOperationException("An active profile is required for authenticated Storage requests.");
        }
        _blobUrl = StorageBlobCustomizationHelpers.BuildBlobUrl(AccountName, ContainerName, BlobName);
        Dictionary<string, string> metadata =
            StorageBlobCustomizationHelpers.ParsePairs(Metadata, nameof(Metadata));
        string? blobTagsString = StorageBlobCustomizationHelpers.SerializeTags(Tags);
        string? blobContentType = null;
        string? blobContentEncoding = null;
        string? blobContentLanguage = null;
        string? blobCacheControl = null;
        string? blobContentDisposition = null;
        byte[]? blobContentMd5 = null;
        foreach (string value in Properties)
        {
            int separator = value.IndexOf('=');
            if (separator <= 0)
            {
                throw new ArgumentException($"Expected KEY=VALUE but received '{value}'.", nameof(Properties));
            }
            string propertyValue = value[(separator + 1)..];
            switch (value[..separator].ToLowerInvariant())
            {
                case "contenttype":
                    blobContentType = propertyValue;
                    break;
                case "contentencoding":
                    blobContentEncoding = propertyValue;
                    break;
                case "contentlanguage":
                    blobContentLanguage = propertyValue;
                    break;
                case "cachecontrol":
                    blobCacheControl = propertyValue;
                    break;
                case "contentdisposition":
                    blobContentDisposition = propertyValue;
                    break;
                case "contentmd5":
                    blobContentMd5 = Convert.FromBase64String(propertyValue);
                    break;
                default:
                    throw new ArgumentException(
                        $"Unsupported blob property '{value[..separator]}'.",
                        nameof(Properties));
            }
        }

        await using FileStream source = File.OpenRead(_sourcePath);
        BlockBlobUploadProcessor processor = CreateExtendedProcessor<BlockBlobUploadProcessor>(p =>
        {
            p.Url = _blobUrl;
            p.Body = source;
            p.ContentLength = source.Length;
            p.IfNoneMatch = Overwrite ? null : "*";
            p.Metadata = metadata;
            p.BlobTagsString = blobTagsString;
            p.BlobContentType = blobContentType;
            p.BlobContentEncoding = blobContentEncoding;
            p.BlobContentLanguage = blobContentLanguage;
            p.BlobCacheControl = blobCacheControl;
            p.BlobContentDisposition = blobContentDisposition;
            p.BlobContentMd5 = blobContentMd5;
            p.Tier = StandardBlobTier;
            p.EncryptionScope = EncryptionScope;
            p.IfTags = TagCondition;
        });

        HttpResponseMessage response;
        try
        {
            response = await processor.SendAsync();
        }
        catch (HttpRequestException exception) when (
            !Overwrite
            && exception.StatusCode == HttpStatusCode.PreconditionFailed)
        {
            throw new HttpRequestException(
                "The blob already exists. Enable overwrite to replace it.",
                exception,
                exception.StatusCode);
        }
        _contentLength = sourceInfo.Length;
        _etag = response.Headers.ETag?.Tag;
        _lastModified = Header(response, "Last-Modified");
        return response;
    }

    protected override Task BeforeWriteOutputAsync(object? body, ref bool returnNow)
    {
        returnNow = true;
        return WriteOutput(
            new BlobTransferResult(
                "upload",
                _sourcePath,
                _blobUrl,
                _etag,
                _lastModified,
                _contentLength),
            Publisher.Token);
    }
}
