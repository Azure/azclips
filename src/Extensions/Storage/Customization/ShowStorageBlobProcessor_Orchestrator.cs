// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;
using BlobModels = Microsoft.Azure.Azclips.Extensions.Storage.Blob.Models;
using BlobProcessors = Microsoft.Azure.Azclips.Extensions.Storage.Blob.Processors;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors;

public sealed class ShowStorageBlobProcessor_Orchestrator : BlobCliOrchestratorBase
{
    public bool IncludeTags { get; set; }

    public bool IncludeTag { get; set; }
    internal bool ReturnCliProperties { get; set; }

    protected override Task ExecuteAsync()
    {
        ResolveCliTarget();
        IncludeTag = IncludeTags;
        ReturnCliProperties = true;
        return ExecuteOperationAsync();
    }

    internal override Task ProcessNativeAsync() => ProcessWorkflowAsync(ExecuteOperationAsync);

    private async Task ExecuteOperationAsync()
    {
        var target = ResolveTarget();
        var blob = await ReadPropertiesAsync(target);
        if (IncludeTag)
        {
            var child = CreateBlobProcessor<BlobProcessors.GetTagsBlobProcessor>(target.Container, processor =>
            {
                processor.Url = target.Url; processor.Timeout = Timeout; processor.LeaseId = LeaseId;
                processor.IfMatch = blob.Properties.ETag; processor.IfNoneMatch = IfNoneMatch;
                processor.IfModifiedSince = Date(IfModifiedSince); processor.IfUnmodifiedSince = Date(IfUnmodifiedSince);
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
