// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;
using BlobModels = Microsoft.Azure.Azclips.Extensions.Storage.Blob.Models;
using BlobProcessors = Microsoft.Azure.Azclips.Extensions.Storage.Blob.Processors;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors;

public sealed class RemoveStorageBlobProcessor_Orchestrator : StorageDataPlaneProcessorBase, IBlobCliProcessor
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

    public string? DeleteSnapshots { get; set; }
    public bool PassThru { get; set; }
    internal Func<string, string, bool> ShouldProcess { get; set; } = (_, _) => true;

    private Task ExecuteAsync()
    {
        BlobCliOperations.ResolveTarget(this);
        return ExecuteOperationAsync();
    }

    internal override Task ProcessNativeAsync() => ProcessWorkflowAsync(ExecuteOperationAsync);

    private async Task ExecuteOperationAsync()
    {
        var target = BlobTargetOperations.Resolve(this, this);
        if (DeleteSnapshots is not null && DeleteSnapshots is not ("include" or "only") ||
            DeleteSnapshots is not null && (target.Snapshot is not null || target.VersionId is not null))
            throw new ArgumentException("Specify snapshot deletion behavior only for a base blob.");
        if (!ShouldProcess(target.Url, "Delete blob")) return;
        var child = BlobContainerOperations.CreateProcessor<BlobProcessors.DeleteBlobProcessor>(this, target.Container, processor =>
        {
            processor.Url = target.Url; processor.Timeout = Timeout; processor.DeleteSnapshots = DeleteSnapshots;
            processor.LeaseId = LeaseId; processor.IfMatch = IfMatch; processor.IfNoneMatch = IfNoneMatch;
            processor.IfModifiedSince = BlobTargetOperations.Date(IfModifiedSince); processor.IfUnmodifiedSince = BlobTargetOperations.Date(IfUnmodifiedSince);
            processor.IfTags = IfTags;
        });
        using var response = await child.SendAsync();
        if (PassThru) await WriteOutput(true, Token);
    }
}
