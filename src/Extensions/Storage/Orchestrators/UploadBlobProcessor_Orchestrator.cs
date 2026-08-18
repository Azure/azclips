// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Storage.Models;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors;

public sealed class UploadBlobProcessor_Orchestrator : StorageBlobProcessorBase
{
    private const long MaxSingleUploadBytes = 5000L * 1024 * 1024;

    public string FilePath { get; set; } = string.Empty;
    public bool Overwrite { get; set; }

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
        if (sourceInfo.Length > MaxSingleUploadBytes)
        {
            throw new InvalidOperationException("Files larger than 5000 MiB require block upload, which is not supported yet.");
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
        });

        using HttpResponseMessage response = await processor.SendAsync();
        if (!Overwrite
            && response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.PreconditionFailed)
        {
            const string message = "The blob already exists. Pass --overwrite to replace it.";
            await Publisher.Publish(Events.ResourceOperationFailed, message);
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
