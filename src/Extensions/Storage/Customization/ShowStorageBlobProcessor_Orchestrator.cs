// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;
using BlobModels = Microsoft.Azure.Azclips.Extensions.Storage.Blob.Models;
using BlobProcessors = Microsoft.Azure.Azclips.Extensions.Storage.Blob.Processors;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors;

public sealed class ShowStorageBlobProcessor_Orchestrator : StorageDataPlaneProcessorBase, IBlobCliProcessor
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

    public bool IncludeTags { get; set; }

    public bool IncludeTag { get; set; }
    internal bool ReturnCliProperties { get; set; }

    private Task ExecuteAsync()
    {
        BlobCliOperations.ResolveTarget(this);
        IncludeTag = IncludeTags;
        ReturnCliProperties = true;
        return ExecuteOperationAsync();
    }

    internal override Task ProcessNativeAsync() => ProcessWorkflowAsync(ExecuteOperationAsync);

    private async Task ExecuteOperationAsync()
    {
        var target = BlobTargetOperations.Resolve(this, this);
        var blob = await BlobTargetOperations.ReadPropertiesAsync(this, target);
        if (IncludeTag)
        {
            var child = BlobContainerOperations.CreateProcessor<BlobProcessors.GetTagsBlobProcessor>(this, target.Container, processor =>
            {
                processor.Url = target.Url; processor.Timeout = Timeout; processor.LeaseId = LeaseId;
                processor.IfMatch = blob.Properties.ETag; processor.IfNoneMatch = IfNoneMatch;
                processor.IfModifiedSince = BlobTargetOperations.Date(IfModifiedSince); processor.IfUnmodifiedSince = BlobTargetOperations.Date(IfUnmodifiedSince);
                processor.IfTags = IfTags;
            });
            using var response = await child.SendAsync();
            var tags = await StorageWireFormat.ReadResponseAsync<BlobModels.BlobTags>(response, Token);
            blob = blob with
            {
                Tags = new ReadOnlyDictionary<string, string>(tags.BlobTagSet.ToDictionary(tag => tag.Key, tag => tag.Value, StringComparer.Ordinal))
            };
        }
        await WriteOutput(ReturnCliProperties ? StorageBlobCliOutput.Project(blob, IncludeTag) : blob, Token);
    }
}
