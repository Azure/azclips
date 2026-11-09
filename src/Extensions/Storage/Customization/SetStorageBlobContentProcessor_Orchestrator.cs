// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Net;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;
using BlobProcessors = Microsoft.Azure.Azclips.Extensions.Storage.Blob.Processors;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors;

public sealed class SetStorageBlobContentProcessor_Orchestrator : StorageDataPlaneProcessorBase, IBlobTargetOptions
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
    public StorageBlobPowerShellPlan? Plan { get; set; }
    protected override StorageService Service => StorageService.Blob;
    public override Task ProcessAsync() => ProcessWorkflowAsync(ExecuteAsync);

    public string File { get; set; } = string.Empty;
    public string BlobType { get; set; } = "Block";
    public bool Force { get; set; }
    public bool ValidateContent { get; set; }
    public int Concurrency { get; set; } = 2;
    public int ChunkSize { get; set; } = 4 * 1024 * 1024;
    public string? ContentType { get; set; }
    public string? ContentEncoding { get; set; }
    public string? ContentLanguage { get; set; }
    public string? ContentDisposition { get; set; }
    public string? CacheControl { get; set; }
    public byte[]? ContentMd5 { get; set; }
    public IReadOnlyDictionary<string, string>? Metadata { get; set; }
    public IReadOnlyDictionary<string, string>? Tags { get; set; }
    public string? Tier { get; set; }

    public StorageBlobPowerShellPlan Prepare()
    {
        if (Context is null && InputObject is null && ContainerObject is null)
            throw new ArgumentException("Specify a native Storage context or resource.");
        string path = Path.GetFullPath(File);
        if (!System.IO.File.Exists(path)) throw new FileNotFoundException("The source file must exist.", path);
        var target = BlobTargetOperations.Resolve(this, this, Path.GetFileName(path));
        var frozen = (SetStorageBlobContentProcessor_Orchestrator)MemberwiseClone();
        frozen.Context = target.Container.Context; frozen.Container = target.Container.Name; frozen.Blob = target.Name;
        frozen.InputObject = null; frozen.ContainerObject = null; frozen.File = path;
        frozen.Metadata = BlobPowerShellOptions.Freeze(Metadata); frozen.Tags = BlobPowerShellOptions.Freeze(Tags);
        frozen.ContentMd5 = ContentMd5?.ToArray();
        if (frozen.ContentEncoding is null)
            frozen.ContentType ??= StorageContentSettings.InferContentType(path);
        if (target.Snapshot is not null || target.VersionId is not null)
            throw new ArgumentException("Uploads must target a base blob.");
        BlobPowerShellOptions.ValidateHeaders(LeaseId, IfMatch, IfNoneMatch, IfTags);
        IBlobUploadProcessor validation = BlobType switch
        {
            "Block" => new UploadStorageBlockBlobWorkflow(),
            "Page" => new UploadStoragePageBlobWorkflow(),
            "Append" => new UploadStorageAppendBlobWorkflow(),
            _ => throw new ArgumentException("Specify Block, Page, or Append.")
        };
        frozen.Apply(validation);
        BlobUploadOperations.ValidatePowerShellUpload(this, validation);
        return new(target.Container, target.Name, target.Url, $"Upload {BlobType} Blob", BlobType, Force, child =>
        {
            BlobPowerShellOptions.Target(frozen, child);
            if (child is IBlobUploadProcessor upload) frozen.Apply(upload);
        });
    }

    private void Apply(IBlobUploadProcessor child)
    {
        BlobPowerShellOptions.Target(this, child);
        child.File = File; child.Overwrite = true; child.ValidateContent = ValidateContent;
        child.Concurrency = Concurrency; child.ChunkSize = ChunkSize;
        child.ContentType = ContentType; child.ContentEncoding = ContentEncoding; child.ContentLanguage = ContentLanguage;
        child.ContentDisposition = ContentDisposition; child.CacheControl = CacheControl; child.ContentMd5 = ContentMd5;
        child.Metadata = Metadata; child.Tags = Tags; child.Tier = Tier;
    }

    private Task ExecuteAsync() => BlobWritePowerShellOperations.ExecutePlanAsync(this, Plan ?? Prepare(), async plan =>
    {
        IBlobUploadProcessor child = plan.Kind switch
        {
            "Block" => BlobContainerOperations.CreateProcessor<UploadStorageBlockBlobWorkflow>(this, plan.Container, processor => BlobWritePowerShellOperations.ConfigureWrite(processor, plan)),
            "Page" => BlobContainerOperations.CreateProcessor<UploadStoragePageBlobWorkflow>(this, plan.Container, processor => BlobWritePowerShellOperations.ConfigureWrite(processor, plan)),
            "Append" => BlobContainerOperations.CreateProcessor<UploadStorageAppendBlobWorkflow>(this, plan.Container, processor => BlobWritePowerShellOperations.ConfigureWrite(processor, plan)),
            _ => throw new ArgumentException("Invalid upload plan.")
        };
        child.Overwrite = true;
        // Set-AzStorageBlobContent replaces Append Blobs; it never appends to an existing destination.
        if (!plan.Force)
            child.IfNoneMatch = "*";
        await child.ProcessAsync();
    });
}
