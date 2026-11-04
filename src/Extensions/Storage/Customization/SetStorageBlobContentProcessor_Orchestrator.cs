// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Net;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;
using BlobProcessors = Microsoft.Azure.Azclips.Extensions.Storage.Blob.Processors;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors;

public sealed class SetStorageBlobContentProcessor_Orchestrator : BlobWritePowerShellOrchestratorBase
{
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
        var target = ResolveTarget(Path.GetFileName(path));
        var frozen = (SetStorageBlobContentProcessor_Orchestrator)MemberwiseClone();
        frozen.Context = target.Container.Context; frozen.Container = target.Container.Name; frozen.Blob = target.Name;
        frozen.InputObject = null; frozen.ContainerObject = null; frozen.File = path;
        frozen.Metadata = BlobPowerShellOptions.Freeze(Metadata); frozen.Tags = BlobPowerShellOptions.Freeze(Tags);
        frozen.ContentMd5 = ContentMd5?.ToArray();
        frozen.ContentType ??= StorageContentSettings.InferContentType(path);
        if (target.Snapshot is not null || target.VersionId is not null)
            throw new ArgumentException("Uploads must target a base blob.");
        BlobPowerShellOptions.ValidateHeaders(LeaseId, IfMatch, IfNoneMatch, IfTags);
        BlobUploadWorkflowBase validation = BlobType switch
        {
            "Block" => new UploadStorageBlockBlobWorkflow(),
            "Page" => new UploadStoragePageBlobWorkflow(),
            "Append" => new UploadStorageAppendBlobWorkflow(),
            _ => throw new ArgumentException("Specify Block, Page, or Append.")
        };
        frozen.Apply(validation);
        validation.ValidatePowerShellUpload();
        return new(target.Container, target.Name, target.Url, $"Upload {BlobType} Blob", BlobType, Force, child =>
        {
            BlobPowerShellOptions.Target(frozen, child);
            if (child is BlobUploadWorkflowBase upload) frozen.Apply(upload);
        });
    }

    private void Apply(BlobUploadWorkflowBase child)
    {
        BlobPowerShellOptions.Target(this, child);
        child.File = File; child.Overwrite = true; child.ValidateContent = ValidateContent;
        child.Concurrency = Concurrency; child.ChunkSize = ChunkSize;
        child.ContentType = ContentType; child.ContentEncoding = ContentEncoding; child.ContentLanguage = ContentLanguage;
        child.ContentDisposition = ContentDisposition; child.CacheControl = CacheControl; child.ContentMd5 = ContentMd5;
        child.Metadata = Metadata; child.Tags = Tags; child.Tier = Tier;
    }

    protected override Task ExecuteAsync() => ExecutePlanAsync(Plan ?? Prepare(), async plan =>
    {
        BlobUploadWorkflowBase child = plan.Kind switch
        {
            "Block" => CreateBlobProcessor<UploadStorageBlockBlobWorkflow>(plan.Container, processor => ConfigureWrite(processor, plan)),
            "Page" => CreateBlobProcessor<UploadStoragePageBlobWorkflow>(plan.Container, processor => ConfigureWrite(processor, plan)),
            "Append" => CreateBlobProcessor<UploadStorageAppendBlobWorkflow>(plan.Container, processor => ConfigureWrite(processor, plan)),
            _ => throw new ArgumentException("Invalid upload plan.")
        };
        child.Overwrite = true;
        // Set-AzStorageBlobContent replaces Append Blobs; it never appends to an existing destination.
        if (!plan.Force)
            child.IfNoneMatch = "*";
        await child.ProcessAsync();
    });
}
