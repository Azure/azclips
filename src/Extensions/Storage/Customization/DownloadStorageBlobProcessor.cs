// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using BlobProcessors = Microsoft.Azure.Azclips.Extensions.Storage.Blob.Processors;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

public sealed class DownloadStorageBlobProcessor : BlobTargetWorkflowBase
{
    public string? BlobUri { get; set; }
    public string? Destination { get; set; }
    public bool Overwrite { get; set; }
    public bool CheckMd5 { get; set; }
    public bool ValidateContent { get; set; }
    public int Concurrency { get; set; } = 2;
    public int ChunkSize { get; set; } = StorageWireFormat.MaximumChunkBytes;
    public long? StartRange { get; set; }
    public long? EndRange { get; set; }
    internal Stream? BinaryOutput { get; set; }
    internal bool SuppressOutput { get; set; }
    internal Func<long, long, bool, Task>? ProgressHandler { get; set; }
    internal Func<string, string, bool> ShouldProcess { get; set; } = (_, _) => true;
    internal bool DestinationOpened { get; private set; }

    protected override async Task ExecuteAsync()
    {
        var target = ResolveTarget(resourceUri: BlobUri);
        StorageDownload.ValidateOptions(Concurrency, ChunkSize, StartRange, EndRange);
        string? destination = null;
        if (BinaryOutput is not null)
        {
            if (Destination is not null || !BinaryOutput.CanWrite)
                throw new ArgumentException("Specify either a writable binary output stream or a destination file.");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(Destination))
                throw new ArgumentException("Specify a resolved destination file or a binary output stream.");
            destination = StorageDownload.ResolveDestination(Destination);
        }
        if (!ShouldProcess(target.Url, "Download blob")) return;
        if (destination is not null && !Overwrite && File.Exists(destination))
            throw new IOException("The destination already exists; specify overwrite or choose another file.");

        var blob = await ReadPropertiesAsync(target);
        long size = blob.Properties.ContentLength is { } length && length >= 0
            ? length : throw new InvalidDataException("Storage did not return a valid blob length.");
        long start = StartRange ?? 0;
        if (StartRange is not null && start >= size)
            throw new ArgumentOutOfRangeException(nameof(StartRange), "The requested range starts outside the blob.");
        long total = size == 0 ? 0 : Math.Min(EndRange ?? (size - 1), size - 1) - start + 1;
        if (CheckMd5 && start == 0 && total == size && blob.Properties.ContentMd5 is { Length: not 16 })
            throw new InvalidDataException("Storage returned an invalid blob Content-MD5 length.");
        if (BinaryOutput is { } output)
            await TransferAsync(output, target, blob, start, total, workers: 1);
        else
        {
            // Match legacy stream ownership/overwrite behavior, including user-created file links.
            await using var file = new FileStream(destination ?? throw new InvalidOperationException("The destination was not resolved."),
                Overwrite ? FileMode.Create : FileMode.CreateNew,
                FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            DestinationOpened = true;
            await TransferAsync(file, target, blob, start, total, Concurrency);
        }
        Token.ThrowIfCancellationRequested();
        await PublishProgressAsync(total, total, completed: true);
        if (BinaryOutput is null && !SuppressOutput) await WriteOutput(blob, Token);
    }

    internal static void ValidateFileName(string name) => StorageDownload.ValidateFileName(name);

    private Task TransferAsync(Stream output, Target target, StorageBlob blob, long start, long total, int workers)
    {
        bool validateRange = CheckMd5 || ValidateContent;
        int chunkSize = validateRange ? Math.Min(ChunkSize, 4 * 1024 * 1024) : ChunkSize;
        bool wholeBlob = start == 0 && total == blob.Properties.ContentLength;
        byte[]? expectedHash = CheckMd5 && wholeBlob ? blob.Properties.ContentMd5 : null;
        return StorageDownload.TransferAsync(output, total, workers, chunkSize, expectedHash,
            (offset, count) => ReadRangeAsync(target, blob, start + offset, count, validateRange),
            PublishProgressAsync, "The downloaded blob did not match its stored Content-MD5.", Token);
    }

    private async Task<byte[]> ReadRangeAsync(Target target, StorageBlob blob, long offset, int count, bool validate)
    {
        var child = CreateBlobProcessor<BlobProcessors.DownloadBlobProcessor>(target.Container, processor =>
        {
            processor.Url = target.Url; processor.Timeout = Timeout; processor.LeaseId = LeaseId;
            processor.IfMatch = blob.Properties.ETag; processor.IfNoneMatch = IfNoneMatch;
            processor.IfModifiedSince = Date(IfModifiedSince); processor.IfUnmodifiedSince = Date(IfUnmodifiedSince);
            processor.IfTags = IfTags; processor.RangeGetContentMd5 = validate ? true : null;
            processor.Range = string.Create(CultureInfo.InvariantCulture, $"bytes={offset}-{offset + count - 1}");
        });
        using var response = await child.SendAsync();
        return await StorageDownload.ReadRangeAsync(response, offset, count,
            blob.Properties.ContentLength!.Value, blob.Properties.ETag, validate, Token);
    }

    private Task PublishProgressAsync(long transferred, long total, bool completed) =>
        SuppressProgress ? Task.CompletedTask : ProgressHandler?.Invoke(transferred, total, completed) ?? Publisher.Publish(Events.Progress, Token, new EventData
        {
            Id = Events.Progress, Message = completed ? "Blob download completed." : $"Downloaded {transferred} of {total} bytes.",
            Value = completed ? 100 : total == 0 ? 0 : Math.Min(99, transferred * 100.0 / total), Cancel = Cancel
        });
}
