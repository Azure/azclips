// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using System.Text.Json;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

public sealed class UploadStorageBlobCommand : BlobCliCommandBase
{
    public string File { get; set; } = string.Empty;
    public string? BlobType { get; set; }
    public bool Overwrite { get; set; }
    public bool ValidateContent { get; set; }
    public int Concurrency { get; set; } = 2;
    public int? ChunkSize { get; set; }
    public bool NoProgress { get; set; }
    public string? ContentType { get; set; }
    public string? ContentEncoding { get; set; }
    public string? ContentLanguage { get; set; }
    public string? ContentDisposition { get; set; }
    public string? CacheControl { get; set; }
    public string? ContentMd5Input { get; set; }
    public string[]? MetadataInput { get; set; }
    public string[]? TagsInput { get; set; }
    public string? Tier { get; set; }
    public long? MaxSizeCondition { get; set; }

    protected override Task ExecuteAsync()
    {
        if (string.IsNullOrWhiteSpace(File)) throw new ArgumentException("Specify a source file.");
        string type = BlobType ?? (File.EndsWith(".vhd", StringComparison.Ordinal) ? "page" : "block");
        var metadata = StorageCliKeyValue.Parse(MetadataInput, tags: false);
        var tags = StorageCliKeyValue.Parse(TagsInput, tags: true);
        var md5 = StorageContentSettings.ParseMd5(ContentMd5Input);
        if (type != "append" && MaxSizeCondition is not null)
            throw new ArgumentException("The maximum-size condition applies only to Append blobs.");
        BlobUploadWorkflowBase processor = type switch
        {
            "block" => CreateTargetWorkflow<UploadStorageBlockBlobProcessor>(defaultName: Path.GetFileName(File)),
            "page" => CreateTargetWorkflow<UploadStoragePageBlobProcessor>(defaultName: Path.GetFileName(File)),
            "append" => CreateTargetWorkflow<UploadStorageAppendBlobProcessor>(child =>
            {
                child.MaxSizeCondition = MaxSizeCondition;
                child.SkipEmptyUpload = true;
            }, Path.GetFileName(File)),
            _ => throw new ArgumentException("Specify blob type block, page or append.")
        };
        processor.File = File; processor.Overwrite = Overwrite; processor.ValidateContent = ValidateContent;
        processor.Concurrency = Concurrency; processor.ChunkSize = ChunkSize ?? (type == "page" ? 4 * 1024 * 1024 : StorageWireFormat.MaximumChunkBytes);
        processor.SuppressProgress = NoProgress;
        processor.ContentType = ContentType ?? (ContentEncoding is null ? StorageContentSettings.InferContentType(File) : null);
        processor.ContentEncoding = ContentEncoding; processor.ContentLanguage = ContentLanguage;
        processor.ContentDisposition = ContentDisposition; processor.CacheControl = CacheControl;
        processor.ContentMd5 = md5; processor.Metadata = metadata; processor.Tags = tags; processor.Tier = Tier;
        processor.OutputOverride = (value, token) => WriteOutput(value switch
        {
            StorageBlob blob => JsonSerializer.SerializeToElement(new
            {
                etag = blob.Properties.ETag,
                lastModified = DateTimeOffset.Parse(blob.Properties.LastModified,
                    System.Globalization.CultureInfo.InvariantCulture).UtcDateTime,
                contentMd5 = blob.Properties.ContentMd5,
                versionId = blob.VersionId,
                requestServerEncrypted = blob.Properties.ServerEncrypted
            }),
            StorageAppendUploadNoOp => JsonSerializer.SerializeToElement(new { }),
            _ => throw new InvalidDataException("The upload returned an unsupported result.")
        }, token);
        return processor.ProcessAsync();
    }
}

public sealed class DownloadStorageBlobCommand : BlobCliCommandBase
{
    public string? File { get; set; }
    public bool Overwrite { get; set; } = true;
    public bool ValidateContent { get; set; }
    public bool CheckMd5 { get; set; }
    public bool NoProgress { get; set; }
    public int Concurrency { get; set; } = 2;
    public int ChunkSize { get; set; } = StorageWireFormat.MaximumChunkBytes;
    public long? StartRange { get; set; }
    public long? EndRange { get; set; }

    protected override async Task ExecuteAsync()
    {
        if (File == string.Empty) throw new ArgumentException("The destination file cannot be empty.");
        var child = CreateTargetWorkflow<DownloadStorageBlobProcessor>(processor =>
        {
            processor.Destination = File; processor.Overwrite = Overwrite;
            processor.Concurrency = Concurrency; processor.ChunkSize = ChunkSize;
            processor.SuppressProgress = NoProgress;
            processor.ValidateContent = ValidateContent; processor.CheckMd5 = CheckMd5;
            processor.StartRange = StartRange; processor.EndRange = EndRange;
            processor.OutputOverride = (value, token) => WriteOutput(value is StorageBlob blob
                ? StorageBlobCliOutput.Project(blob, includeTags: false)
                : throw new InvalidDataException("The download returned an unsupported result."), token);
        });
        if (File is not null) { await child.ProcessAsync(); return; }
        using var stream = new BinaryStream(WriteOutput);
        child.BinaryOutput = stream;
        await child.ProcessAsync();
    }

    private sealed class BinaryStream(OutputStreamDelegate output) : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
            new(output(new BinaryOutputChunk(buffer.ToArray()), cancellationToken));
    }
}

public sealed class DownloadStorageBlobBatchCommand : BlobContainerWorkflowBase
{
    public string AuthMode { get; set; } = "login";
    public string? BlobEndpoint { get; set; }
    public string? Destination { get; set; }
    public string? Pattern { get; set; }
    public bool Overwrite { get; set; }
    public bool DryRun { get; set; }
    public bool ValidateContent { get; set; }
    public bool CheckMd5 { get; set; }
    public bool NoProgress { get; set; }
    public int Concurrency { get; set; } = 2;
    public int ChunkSize { get; set; } = StorageWireFormat.MaximumChunkBytes;
    public string? LeaseId { get; set; }
    public string? IfMatch { get; set; }
    public string? IfNoneMatch { get; set; }
    public string? IfModifiedSinceInput { get; set; }
    public string? IfUnmodifiedSinceInput { get; set; }
    public string? IfTags { get; set; }

    protected override Task ExecuteAsync()
    {
        string? account = StorageBlobCliAddress.ResolveAccount(StorageAccountName, BlobEndpoint, AuthMode);
        var container = ResolveContainer(defaultAccount: account);
        StorageBlobCliAddress.ValidateContext(account, container.Context);
        var filter = StorageCliPattern.Compile(Pattern);
        var modified = BlobCliCommandBase.ParseDate(IfModifiedSinceInput);
        var unmodified = BlobCliCommandBase.ParseDate(IfUnmodifiedSinceInput);
        var names = new List<string>();
        var child = CreateExtendedProcessor<DownloadStorageBlobBatchProcessor>(processor =>
        {
            processor.ContainerObject = container; processor.Context = container.Context; processor.Timeout = Timeout;
            processor.Destination = Destination; processor.Overwrite = Overwrite; processor.DryRun = DryRun;
            processor.ValidateContent = ValidateContent; processor.CheckMd5 = CheckMd5;
            processor.Concurrency = Concurrency; processor.ChunkSize = ChunkSize;
            processor.SuppressProgress = NoProgress;
            processor.LeaseId = LeaseId; processor.IfMatch = IfMatch; processor.IfNoneMatch = IfNoneMatch;
            processor.IfModifiedSince = modified; processor.IfUnmodifiedSince = unmodified; processor.IfTags = IfTags;
            processor.NameFilter = filter; processor.DeferFailureDiagnostics = true;
            processor.OutputOverride = async (value, token) =>
            {
                token.ThrowIfCancellationRequested();
                string name = value as string ?? throw new InvalidDataException("A batch download returned an unsupported result.");
                names.Add(name);
                await Publisher.Publish(Events.Information, "Downloaded blob " + JsonSerializer.Serialize(name));
            };
        });
        return Run();

        async Task Run()
        {
            await child.ProcessAsync();
            if (!child.DryRun) await WriteOutput(names.ToArray(), Token);
        }
    }

}
