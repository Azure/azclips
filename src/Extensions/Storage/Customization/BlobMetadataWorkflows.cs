// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using BlobModels = Microsoft.Azure.Azclips.Extensions.Storage.Blob.Models;
using BlobProcessors = Microsoft.Azure.Azclips.Extensions.Storage.Blob.Processors;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

public sealed class GetStorageBlobProcessor : BlobTargetWorkflowBase
{
    public bool IncludeTag { get; set; }
    internal bool ReturnCliProperties { get; set; }

    protected override async Task ExecuteAsync()
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

public sealed class TestStorageBlobExistsProcessor : BlobTargetWorkflowBase
{
    protected override async Task ExecuteAsync()
    {
        var target = ResolveTarget();
        bool exists;
        try
        {
            var properties = PropertiesProcessor(target);
            properties.DeferFailureDiagnostics = true;
            using var response = await properties.SendAsync();
            exists = true;
        }
        catch (HttpRequestException exception) when (IsMissingBlob(exception) ||
            exception.StatusCode == System.Net.HttpStatusCode.NotFound && exception.Data["ServiceErrorCode"] is "ContainerNotFound")
        {
            exists = false;
        }
        await WriteOutput(new StorageBlobExistence(exists), Token);
    }
}

public sealed class RemoveStorageBlobProcessor : BlobTargetWorkflowBase
{
    public string? DeleteSnapshots { get; set; }
    public bool PassThru { get; set; }
    internal Func<string, string, bool> ShouldProcess { get; set; } = (_, _) => true;

    protected override async Task ExecuteAsync()
    {
        var target = ResolveTarget();
        if (DeleteSnapshots is not null && DeleteSnapshots is not ("include" or "only") ||
            DeleteSnapshots is not null && (target.Snapshot is not null || target.VersionId is not null))
            throw new ArgumentException("Specify snapshot deletion behavior only for a base blob.");
        if (!ShouldProcess(target.Url, "Delete blob")) return;
        var child = CreateBlobProcessor<BlobProcessors.DeleteBlobProcessor>(target.Container, processor =>
        {
            processor.Url = target.Url; processor.Timeout = Timeout; processor.DeleteSnapshots = DeleteSnapshots;
            processor.LeaseId = LeaseId; processor.IfMatch = IfMatch; processor.IfNoneMatch = IfNoneMatch;
            processor.IfModifiedSince = Date(IfModifiedSince); processor.IfUnmodifiedSince = Date(IfUnmodifiedSince);
            processor.IfTags = IfTags;
        });
        using var response = await child.SendAsync();
        if (PassThru) await WriteOutput(true, Token);
    }
}

public sealed class SetStorageBlobTierProcessor : BlobTargetWorkflowBase
{
    internal static readonly string[] BlockTiers = ["Hot", "Cool", "Cold", "Archive"];
    internal static readonly string[] PageTiers = ["P4", "P6", "P10", "P15", "P20", "P30", "P40", "P50", "P60", "P70", "P80"];
    public string Tier { get; set; } = string.Empty;
    public string? RehydratePriority { get; set; }
    internal string? ExpectedBlobType { get; set; }
    internal Func<string, string, bool> ShouldProcess { get; set; } = (_, _) => true;

    protected override async Task ExecuteAsync()
    {
        var target = ResolveTarget();
        if (IfMatch is not null || IfNoneMatch is not null || IfModifiedSince is not null || IfUnmodifiedSince is not null)
            throw new ArgumentException("Setting a blob tier supports lease and tag conditions only.");
        if (!BlockTiers.Contains(Tier, StringComparer.Ordinal) && !PageTiers.Contains(Tier, StringComparer.Ordinal) ||
            RehydratePriority is not null && RehydratePriority is not ("High" or "Standard"))
            throw new ArgumentException("Specify a supported Block/Page tier and rehydration priority.");
        if (!ShouldProcess(target.Url, "Set blob tier")) return;
        var blob = await ReadPropertiesAsync(target);
        if (ExpectedBlobType is not null && ExpectedBlobType != blob.Properties.BlobType ||
            blob.Properties.BlobType is not ("BlockBlob" or "PageBlob") ||
            !(blob.Properties.BlobType == "BlockBlob" ? BlockTiers : PageTiers).Contains(Tier, StringComparer.Ordinal) ||
            blob.Properties.BlobType == "PageBlob" && RehydratePriority is not null)
            throw new ArgumentException("The requested tier or rehydration priority does not apply to this blob type.");
        var child = CreateBlobProcessor<BlobProcessors.SetTierBlobProcessor>(target.Container, processor =>
        {
            processor.Url = target.Url; processor.Timeout = Timeout; processor.Tier = Tier;
            processor.RehydratePriority = RehydratePriority; processor.LeaseId = LeaseId; processor.IfTags = IfTags;
        });
        using var response = await child.SendAsync();
    }
}
