// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Storage.Models;
using System.Security.Cryptography;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors;

public sealed class BlobDownloadProcessor_Orchestrator : StorageBlobProcessorBase<object, Stream>
{
    private string _destinationPath = string.Empty;
    private string _sourceUrl = string.Empty;
    private string _requestUrl = string.Empty;
    private byte[]? _expectedMd5;
    private string? _etag;
    private string? _lastModified;
    private long? _contentLength;

    public string AccountName { get; set; } = string.Empty;
    public string ContainerName { get; set; } = string.Empty;
    public string BlobName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public bool Overwrite { get; set; }
    public string? AbsoluteUri { get; set; }
    public string? SnapshotTime { get; set; }
    public string? VersionId { get; set; }
    public string? TagCondition { get; set; }
    public bool CheckMd5 { get; set; }

    protected override Task BeforeProcessAsync()
    {
        _destinationPath = ResolveDestinationPath();
        string? destinationDirectory = Path.GetDirectoryName(_destinationPath);
        if (string.IsNullOrEmpty(destinationDirectory) || !Directory.Exists(destinationDirectory))
        {
            throw new DirectoryNotFoundException($"Destination directory for '{_destinationPath}' does not exist.");
        }
        if (!Overwrite && File.Exists(_destinationPath))
        {
            throw new IOException($"Destination file '{_destinationPath}' already exists. Enable overwrite to replace it.");
        }
        if (!string.IsNullOrWhiteSpace(SnapshotTime) && !string.IsNullOrWhiteSpace(VersionId))
        {
            throw new ArgumentException("SnapshotTime and VersionId cannot be used together.");
        }
        if (!string.IsNullOrWhiteSpace(SnapshotTime)
            && !DateTimeOffset.TryParse(SnapshotTime, out _))
        {
            throw new ArgumentException("SnapshotTime must be a valid timestamp.", nameof(SnapshotTime));
        }

        if (!string.IsNullOrWhiteSpace(AbsoluteUri))
        {
            _sourceUrl = StorageBlobCustomizationHelpers.ValidateBlobUrl(AbsoluteUri);
        }
        else
        {
            if (ResolvedProfile is null)
            {
                throw new InvalidOperationException("An active profile is required for authenticated Storage requests.");
            }
            _sourceUrl = StorageBlobCustomizationHelpers.BuildBlobUrl(
                AccountName,
                ContainerName,
                BlobName);
        }

        _requestUrl = string.IsNullOrWhiteSpace(SnapshotTime)
            ? string.IsNullOrWhiteSpace(VersionId)
                ? _sourceUrl
                : $"{_sourceUrl}{(_sourceUrl.Contains('?', StringComparison.Ordinal) ? '&' : '?')}versionid={Uri.EscapeDataString(VersionId)}"
            : $"{_sourceUrl}{(_sourceUrl.Contains('?', StringComparison.Ordinal) ? '&' : '?')}snapshot={Uri.EscapeDataString(SnapshotTime)}";
        return base.BeforeProcessAsync();
    }

    internal override async Task<HttpResponseMessage> SendAsync()
    {
        BlobDownloadProcessor processor = CreateExtendedProcessor<BlobDownloadProcessor>(p =>
        {
            p.Url = _sourceUrl;
            p.UseProfileAuthentication = string.IsNullOrWhiteSpace(AbsoluteUri);
            p.Snapshot = SnapshotTime;
            p.VersionId = VersionId;
            p.IfTags = TagCondition;
        });

        HttpResponseMessage response = await processor.SendAsync();
        _expectedMd5 = response.Content.Headers.ContentMD5;
        _etag = response.Headers.ETag?.Tag;
        _lastModified = Header(response, "Last-Modified");
        _contentLength = response.Content.Headers.ContentLength;
        return response;
    }

    protected override Task BeforeWriteOutputAsync(Stream? body, ref bool returnNow)
    {
        returnNow = true;
        return body is null
            ? throw new InvalidDataException("The blob download response did not contain a body.")
            : WriteDownloadAsync(body);
    }

    private async Task WriteDownloadAsync(Stream body)
    {
        string destinationDirectory = Path.GetDirectoryName(_destinationPath)!;
        string temporaryPath = Path.Combine(
            destinationDirectory,
            $"{Path.GetFileName(_destinationPath)}.azclips-download-{Guid.NewGuid():N}");
        try
        {
            {
                await using FileStream destination = new(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None);
                if (CheckMd5)
                {
#pragma warning disable CA5351 // Azure Blob Content-MD5 is a transport checksum, not a security primitive.
                    using var md5 = MD5.Create();
#pragma warning restore CA5351
                    await using var hashingStream = new CryptoStream(destination, md5, CryptoStreamMode.Write, leaveOpen: true);
                    await body.CopyToAsync(hashingStream, Publisher.Token);
                    hashingStream.FlushFinalBlock();
                    ValidateMd5(_expectedMd5, md5.Hash!);
                }
                else
                {
                    await body.CopyToAsync(destination, Publisher.Token);
                }
                await destination.FlushAsync(Publisher.Token);
            }

            File.Move(temporaryPath, _destinationPath, Overwrite);
        }
        finally
        {
            File.Delete(temporaryPath);
        }

        await WriteOutput(
            new BlobTransferResult(
                "download",
                new Uri(_requestUrl).GetLeftPart(UriPartial.Path),
                _destinationPath,
                _etag,
                _lastModified,
                _contentLength),
            Publisher.Token);
    }

    private string ResolveDestinationPath()
    {
        string path = string.IsNullOrWhiteSpace(FilePath) ? Directory.GetCurrentDirectory() : Path.GetFullPath(FilePath);
        if (!Directory.Exists(path))
        {
            return path;
        }

        string sourceName = !string.IsNullOrWhiteSpace(AbsoluteUri)
            ? Path.GetFileName(Uri.UnescapeDataString(new Uri(AbsoluteUri).AbsolutePath))
            : Path.GetFileName(BlobName.Replace('/', Path.DirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(sourceName))
        {
            throw new ArgumentException("The blob URL or name does not contain a file name.");
        }

        string safeName = string.Concat(sourceName.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        if (!string.IsNullOrWhiteSpace(SnapshotTime))
        {
            safeName = $"{safeName}.{DateTimeOffset.Parse(SnapshotTime).UtcDateTime:yyyyMMddHHmmssfff}";
        }
        return Path.Combine(path, safeName);
    }

    private static void ValidateMd5(byte[]? expected, byte[] actual)
    {
        if (expected is not null && !CryptographicOperations.FixedTimeEquals(expected, actual))
        {
            throw new InvalidDataException("Downloaded content does not match the blob MD5 checksum.");
        }
    }
}
