// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Net;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using FileProcessors = Microsoft.Azure.Azclips.Extensions.Storage.Files.Processors;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

public sealed class DownloadStorageFileProcessor : FileShareWorkflowBase
{
    public StorageFile? FileObject { get; set; }
    public string? Destination { get; set; }
    public bool Overwrite { get; set; }
    public bool CheckMd5 { get; set; }
    public bool ValidateContent { get; set; }
    public bool NoProgress { get; set; }
    public bool PassThru { get; set; }
    public int Concurrency { get; set; } = 2;
    public int ChunkSize { get; set; } = 4 * 1024 * 1024;
    public long? StartRange { get; set; }
    public long? EndRange { get; set; }
    public string? LeaseId { get; set; }
    internal bool ReturnProperties { get; set; }
    internal bool Append { get; set; }
    internal Func<string, string, bool> ShouldProcess { get; set; } = (_, _) => true;
    internal Func<string, CancellationToken, Task<bool>>? ConfirmOverwrite { get; set; }

    protected override async Task ExecuteAsync()
    {
        if (FileObject is null && string.IsNullOrEmpty(Path) || FileObject is not null && DirectoryObject is not null)
            throw new ArgumentException("Specify a file path or native file, not a file and directory together.");
        string filePath = FileObject?.Path ?? Path!;
        if (filePath.EndsWith('/'))
            throw new ArgumentException("The source must identify a file, not a directory.");
        var target = ResolveDirectory(filePath, FileObject?.Share);
        if (target.Path.Length == 0 || FileObject is not null && Path is not null && Path.Trim('/') != target.Path)
            throw new ArgumentException("Specify a non-root file path consistent with the native file.");
        StorageDownload.ValidateOptions(Concurrency, ChunkSize, StartRange, EndRange);
        if (PassThru && ReturnProperties || Append && (Concurrency != 1 || Overwrite || ConfirmOverwrite is not null))
            throw new ArgumentException("Choose one output mode; append requires one worker and no overwrite/confirmation mode.");
        if (LeaseId is not null && (LeaseId.Length == 0 || LeaseId.Any(character => character < 0x20 || character > 0x7e)))
            throw new ArgumentException("Lease IDs must contain printable ASCII header values.");
        string destination = StorageDownload.ResolveDestination(Destination);
        if (!ShouldProcess(destination, "Download file")) return;
        bool overwrite = Overwrite;
        if (!Append && File.Exists(destination))
        {
            if (ConfirmOverwrite is not null)
            {
                if (!await ConfirmOverwrite(destination, Token)) return;
                overwrite = true;
            }
            else if (!overwrite)
                throw new IOException("The destination already exists; specify overwrite or choose another file.");
        }

        var file = await ReadPropertiesAsync(target);
        Token.ThrowIfCancellationRequested();
        long size = file.Properties.ContentLength!.Value;
        long start = StartRange ?? 0;
        if (StartRange is not null && start >= size)
            throw new ArgumentOutOfRangeException(nameof(StartRange), "The requested range starts outside the file.");
        long total = size == 0 ? 0 : Math.Min(EndRange ?? (size - 1), size - 1) - start + 1;
        bool validateRange = CheckMd5 || ValidateContent;
        int chunkSize = validateRange ? Math.Min(ChunkSize, 4 * 1024 * 1024) : ChunkSize;
        byte[]? expectedHash = CheckMd5 && start == 0 && total == size ? file.Properties.ContentMd5 : null;
        // Preserve legacy direct-write/link behavior; errors may leave partial local content.
        await using (var output = new FileStream(destination, Append ? FileMode.Append : overwrite ? FileMode.Create : FileMode.CreateNew,
            FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            await StorageDownload.TransferAsync(output, total, Concurrency, chunkSize, expectedHash,
                (offset, count) => ReadRangeAsync(target, file, start + offset, count, validateRange),
                PublishProgressAsync, "The downloaded file did not match its stored Content-MD5.", Token);
        }
        if (PassThru || ReturnProperties)
        {
            var verified = await ReadPropertiesAsync(target);
            if (verified.Properties.ContentLength != size || verified.Properties.ETag != file.Properties.ETag ||
                file.Properties.FileId is not null && verified.Properties.FileId != file.Properties.FileId ||
                !HashesEqual(verified.Properties.ContentMd5, file.Properties.ContentMd5))
                throw new InvalidDataException("The source file properties changed during download.");
            file = verified;
        }
        Token.ThrowIfCancellationRequested();
        await PublishProgressAsync(total, total, completed: true);
        if (PassThru || ReturnProperties) await WriteOutput(file, Token);
    }

    private async Task<StorageFile> ReadPropertiesAsync(StorageFileDirectory target)
    {
        var child = CreateFileProcessor<FileProcessors.GetPropertiesFileProcessor>(target, processor =>
        {
            processor.Url = target.Uri.AbsoluteUri; processor.Timeout = Timeout;
            processor.LeaseId = LeaseId; processor.AllowTrailingDot = AllowTrailingDot;
        });
        using var response = await child.SendAsync();
        if (response.StatusCode != HttpStatusCode.OK)
            throw new InvalidDataException("Storage did not return file properties.");
        var properties = StorageFileProperties.FromHeaders(StorageResourceProperties.FromResponse(response), directory: false);
        return new StorageFile(target.Path, target.Share) { Properties = properties };
    }

    private async Task<byte[]> ReadRangeAsync(StorageFileDirectory target, StorageFile file, long offset, int count, bool validate)
    {
        var child = CreateFileProcessor<FileProcessors.DownloadFileProcessor>(target, processor =>
        {
            processor.Url = target.Uri.AbsoluteUri; processor.Timeout = Timeout;
            processor.LeaseId = LeaseId; processor.AllowTrailingDot = AllowTrailingDot;
            processor.Range = string.Create(CultureInfo.InvariantCulture, $"bytes={offset}-{offset + count - 1}");
            processor.RangeGetContentMD5 = validate ? true : null;
        });
        using var response = await child.SendAsync();
        return await StorageDownload.ReadRangeAsync(response, offset, count,
            file.Properties.ContentLength!.Value, file.Properties.ETag, validate, Token);
    }

    private static bool HashesEqual(byte[]? first, byte[]? second) =>
        first is null ? second is null : second is not null && first.AsSpan().SequenceEqual(second);

    private Task PublishProgressAsync(long transferred, long total, bool completed) =>
        NoProgress || SuppressProgress ? Task.CompletedTask : Publisher.Publish(Events.Progress, Token, new EventData
        {
            Id = Events.Progress, Message = completed ? "File download completed." : $"Downloaded {transferred} of {total} bytes.",
            Value = completed ? 100 : total == 0 ? 0 : Math.Min(99, transferred * 100.0 / total), Cancel = Cancel
        });
}
