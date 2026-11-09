// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Storage.Customization;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors;

public sealed class GetStorageBlobsProcessor_Orchestrator : StorageDataPlaneProcessorBase, IBlobTargetOptions
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
    protected override StorageService Service => StorageService.Blob;
    public override Task ProcessAsync() => ProcessWorkflowAsync(ExecuteAsync);

    public bool ListMode { get; set; }
    public string? Prefix { get; set; }
    public Func<string, bool>? NameFilter { get; set; }
    public int MaxCount { get; set; } = int.MaxValue;
    public StorageBlobContinuationToken? ContinuationToken { get; set; }
    public bool IncludeDeleted { get; set; }
    public bool IncludeVersion { get; set; }
    public bool IncludeTag { get; set; }

    private async Task ExecuteAsync()
    {
        if (ListMode)
        {
            if (Blob is not null || InputObject is not null || Snapshot is not null || VersionId is not null || IfTags is not null)
                throw new ArgumentException("Listing cannot be combined with an exact blob, selector or tag condition.");
            var container = BlobContainerOperations.Resolve(this, this);
            var child = BlobContainerOperations.CreateProcessor<ListStorageBlobsProcessor_Orchestrator>(this, container, processor =>
            {
                processor.ContainerObject = container;
                processor.Prefix = Prefix;
                processor.NameFilter = NameFilter;
                processor.MaxCount = MaxCount;
                processor.ContinuationToken = ContinuationToken;
                processor.Timeout = Timeout;
                processor.Datasets = new List<string> { "metadata", "copy", "snapshots" };
                if (IncludeDeleted) processor.Datasets.Add("deleted");
                if (IncludeVersion) processor.Datasets.Add("versions");
                if (IncludeTag) processor.Datasets.Add("tags");
                // PowerShell carries continuation on each native blob, not in CLI warnings.
                processor.NextMarkerHandler = _ => { };
                processor.DeferFailureDiagnostics = true;
            });
            await child.ProcessNativeAsync();
        }
        else
        {
            if (Prefix is not null || NameFilter is not null || ContinuationToken is not null ||
                IncludeDeleted || IncludeVersion || MaxCount != int.MaxValue)
                throw new ArgumentException("Listing controls require a name pattern or prefix listing.");
            var target = BlobTargetOperations.Resolve(this, this);
            var child = BlobContainerOperations.CreateProcessor<ShowStorageBlobProcessor_Orchestrator>(this, target.Container, processor =>
            {
                processor.ContainerObject = target.Container;
                processor.Blob = target.Name;
                processor.Snapshot = target.Snapshot;
                processor.VersionId = target.VersionId;
                processor.IncludeTag = IncludeTag;
                processor.IfTags = IfTags;
                processor.Timeout = Timeout;
                processor.DeferFailureDiagnostics = true;
            });
            await child.ProcessNativeAsync();
        }
    }
}
