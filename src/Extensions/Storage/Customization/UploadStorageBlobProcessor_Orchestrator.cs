// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors;

public sealed class UploadStorageBlobProcessor_Orchestrator : StorageDataPlaneProcessorBase, IBlobCliProcessor
{
    public string? StorageAccountName { get; set; }
    public string? Container { get; set; }
    public StorageBlobContainer? ContainerObject { get; set; }
    public int? Timeout { get; set; }
    public string? Blob { get; set; }
    public StorageBlob? InputObject { get; set; }
    public string? Snapshot { get; set; }
    public string? VersionId { get; set; }
    public string? LeaseId { get; set; }
    public string? IfMatch { get; set; }
    public string? IfNoneMatch { get; set; }
    public DateTimeOffset? IfModifiedSince { get; set; }
    public DateTimeOffset? IfUnmodifiedSince { get; set; }
    public string? IfTags { get; set; }
    public string AuthMode { get; set; } = "login";
    public string? BlobEndpoint { get; set; }
    public string? IfModifiedSinceInput { get; set; }
    public string? IfUnmodifiedSinceInput { get; set; }
    protected override StorageService Service => StorageService.Blob;
    public override Task ProcessAsync() => ProcessWorkflowAsync(ExecuteAsync);

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

    private Task ExecuteAsync()
    {
        if (string.IsNullOrWhiteSpace(File)) throw new ArgumentException("Specify a source file.");
        string type = BlobType ?? (File.EndsWith(".vhd", StringComparison.Ordinal) ? "page" : "block");
        var metadata = StorageCliKeyValue.Parse(MetadataInput, tags: false);
        var tags = StorageCliKeyValue.Parse(TagsInput, tags: true);
        var md5 = StorageContentSettings.ParseMd5(ContentMd5Input);
        if (type != "append" && MaxSizeCondition is not null)
            throw new ArgumentException("The maximum-size condition applies only to Append blobs.");
        SuppressProgress = NoProgress;
        IBlobUploadProcessor processor = type switch
        {
            "block" => BlobCliOperations.CreateWorkflow<UploadStorageBlockBlobWorkflow>(this, this,
                defaultName: Path.GetFileName(File), output: WriteUploadOutputAsync),
            "page" => BlobCliOperations.CreateWorkflow<UploadStoragePageBlobWorkflow>(this, this,
                defaultName: Path.GetFileName(File), output: WriteUploadOutputAsync),
            "append" => BlobCliOperations.CreateWorkflow<UploadStorageAppendBlobWorkflow>(this, this, child =>
            {
                child.MaxSizeCondition = MaxSizeCondition;
                child.SkipEmptyUpload = true;
            }, Path.GetFileName(File), output: WriteUploadOutputAsync),
            _ => throw new ArgumentException("Specify blob type block, page or append.")
        };
        processor.File = File; processor.Overwrite = Overwrite; processor.ValidateContent = ValidateContent;
        processor.Concurrency = Concurrency; processor.ChunkSize = ChunkSize ?? (type == "page" ? 4 * 1024 * 1024 : StorageWireFormat.MaximumChunkBytes);
        processor.ContentType = ContentType ?? (ContentEncoding is null ? StorageContentSettings.InferContentType(File) : null);
        processor.ContentEncoding = ContentEncoding; processor.ContentLanguage = ContentLanguage;
        processor.ContentDisposition = ContentDisposition; processor.CacheControl = CacheControl;
        processor.ContentMd5 = md5; processor.Metadata = metadata; processor.Tags = tags; processor.Tier = Tier;
        return processor.ProcessAsync();
    }

    private Task WriteUploadOutputAsync(object? value, CancellationToken token) => WriteOutput(value switch
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
}
