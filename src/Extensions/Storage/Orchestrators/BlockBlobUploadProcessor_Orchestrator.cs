// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Storage.Models;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors;

public sealed class BlockBlobUploadProcessor_Orchestrator : StorageBlobProcessorBase
{
    public string FilePath { get; set; } = string.Empty;
    public bool Overwrite { get; set; }
    public string BlobType { get; set; } = "Block";
    public string[] Metadata { get; set; } = [];
    public string[] Tags { get; set; } = [];
    public string[] Properties { get; set; } = [];
    public string? StandardBlobTier { get; set; }
    public string? PremiumPageBlobTier { get; set; }
    public string? EncryptionScope { get; set; }
    public string? TagCondition { get; set; }

    public override async Task ProcessAsync()
    {
        await Publisher.Publish(Events.ProcessorProcessAsyncStart);
        if (string.IsNullOrWhiteSpace(FilePath))
        {
            throw new ArgumentException("FilePath is required.", nameof(FilePath));
        }

        string sourcePath = Path.GetFullPath(FilePath);
        var sourceInfo = new FileInfo(sourcePath);
        if (!sourceInfo.Exists)
        {
            throw new FileNotFoundException($"Upload file '{sourcePath}' does not exist.", sourcePath);
        }
        if (string.IsNullOrWhiteSpace(BlobName))
        {
            BlobName = Path.GetFileName(sourcePath);
        }
        await using FileStream source = File.OpenRead(sourcePath);
        BlockBlobUploadProcessor processor = CreateExtendedProcessor<BlockBlobUploadProcessor>(p =>
        {
            p.AccountName = AccountName;
            p.ContainerName = ContainerName;
            p.BlobName = BlobName;
            p.Content = source;
            p.ContentLength = source.Length;
            p.Overwrite = Overwrite;
            p.BlobType = BlobType;
            p.Metadata = Metadata;
            p.Tags = Tags;
            p.Properties = Properties;
            p.StandardBlobTier = StandardBlobTier;
            p.PremiumPageBlobTier = PremiumPageBlobTier;
            p.EncryptionScope = EncryptionScope;
            p.TagCondition = TagCondition;
        });

        using HttpResponseMessage response = await processor.SendAsync();
        if (!Overwrite
            && response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.PreconditionFailed)
        {
            const string message = "The blob already exists. Pass --overwrite to replace it.";
            throw new HttpRequestException(message, null, response.StatusCode);
        }
        await response.EnsureSuccessAsync(Publisher);

        await WriteOutput(
            new BlobTransferResult(
                "upload",
                sourcePath,
                BuildBlobUri().ToString(),
                response.Headers.ETag?.Tag,
                Header(response, "Last-Modified"),
                sourceInfo.Length),
            Publisher.Token);
        await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
    }
}
