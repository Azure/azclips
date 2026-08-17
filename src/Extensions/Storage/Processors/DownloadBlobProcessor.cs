// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors;

public sealed class DownloadBlobProcessor : StorageBlobProcessorBase
{
    public string FilePath { get; set; } = string.Empty;
    public bool Overwrite { get; set; }

    public override async Task ProcessAsync()
    {
        await Publisher.Publish(Events.ProcessorProcessAsyncStart);
        if (string.IsNullOrWhiteSpace(FilePath))
        {
            throw new ArgumentException("FilePath is required.", nameof(FilePath));
        }

        string destinationPath = Path.GetFullPath(FilePath);
        string? destinationDirectory = Path.GetDirectoryName(destinationPath);
        if (string.IsNullOrEmpty(destinationDirectory) || !Directory.Exists(destinationDirectory))
        {
            throw new DirectoryNotFoundException($"Destination directory for '{destinationPath}' does not exist.");
        }
        if (!Overwrite && File.Exists(destinationPath))
        {
            throw new IOException($"Destination file '{destinationPath}' already exists. Pass --overwrite to replace it.");
        }

        using HttpRequestMessage request = CreateRequest(HttpMethod.Get);
        using HttpResponseMessage response = await SendAsync(request);
        await response.EnsureSuccessAsync(Publisher);

        string temporaryPath = Path.Combine(
            destinationDirectory,
            $"{Path.GetFileName(destinationPath)}.azclips-download-{Guid.NewGuid():N}");
        try
        {
            await using (FileStream destination = new(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None))
            {
                await response.Content.CopyToAsync(destination, Publisher.Token);
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
                request.RequestUri!.ToString(),
                destinationPath,
                response.Headers.ETag?.Tag,
                Header(response, "Last-Modified"),
                response.Content.Headers.ContentLength),
            Publisher.Token);
        await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
    }
}
