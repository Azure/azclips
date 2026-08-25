// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Storage.Models;
using System.Security.Cryptography;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors;

public sealed class BlobDownloadProcessor_Orchestrator : StorageBlobProcessorBase
{
    public string FilePath { get; set; } = string.Empty;
    public bool Overwrite { get; set; }
    public string? AbsoluteUri { get; set; }
    public string? SnapshotTime { get; set; }
    public string? VersionId { get; set; }
    public string? TagCondition { get; set; }
    public bool CheckMd5 { get; set; }

    public override async Task ProcessAsync()
    {
        await Publisher.Publish(Events.ProcessorProcessAsyncStart);
        string destinationPath = ResolveDestinationPath();
        string? destinationDirectory = Path.GetDirectoryName(destinationPath);
        if (string.IsNullOrEmpty(destinationDirectory) || !Directory.Exists(destinationDirectory))
        {
            throw new DirectoryNotFoundException($"Destination directory for '{destinationPath}' does not exist.");
        }
        if (!Overwrite && File.Exists(destinationPath))
        {
            throw new IOException($"Destination file '{destinationPath}' already exists. Pass --overwrite to replace it.");
        }

        BlobDownloadProcessor processor = CreateExtendedProcessor<BlobDownloadProcessor>(p =>
        {
            p.AccountName = AccountName;
            p.ContainerName = ContainerName;
            p.BlobName = BlobName;
            p.AbsoluteUri = AbsoluteUri;
            p.SnapshotTime = SnapshotTime;
            p.VersionId = VersionId;
            p.TagCondition = TagCondition;
        });

        using HttpResponseMessage response = await processor.SendAsync();
        await response.EnsureSuccessAsync(Publisher);

        string temporaryPath = Path.Combine(
            destinationDirectory,
            $"{Path.GetFileName(destinationPath)}.azclips-download-{Guid.NewGuid():N}");
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
                    await response.Content.CopyToAsync(hashingStream, Publisher.Token);
                    hashingStream.FlushFinalBlock();
                    ValidateMd5(response, md5.Hash!);
                }
                else
                {
                    await response.Content.CopyToAsync(destination, Publisher.Token);
                }
                await destination.FlushAsync(Publisher.Token);
            }

            File.Move(temporaryPath, destinationPath, Overwrite);
        }
        finally
        {
            File.Delete(temporaryPath);
        }

        await WriteOutput(
            new BlobTransferResult(
                "download",
                processor.BuildSourceUri().ToString(),
                destinationPath,
                response.Headers.ETag?.Tag,
                Header(response, "Last-Modified"),
                response.Content.Headers.ContentLength),
            Publisher.Token);
        await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
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

    private static void ValidateMd5(HttpResponseMessage response, byte[] actual)
    {
        byte[]? expected = response.Content.Headers.ContentMD5;
        if (expected is not null && !CryptographicOperations.FixedTimeEquals(expected, actual))
        {
            throw new InvalidDataException("Downloaded content does not match the blob MD5 checksum.");
        }
    }
}
