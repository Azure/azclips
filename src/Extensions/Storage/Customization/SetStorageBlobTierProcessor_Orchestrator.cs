// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;
using BlobModels = Microsoft.Azure.Azclips.Extensions.Storage.Blob.Models;
using BlobProcessors = Microsoft.Azure.Azclips.Extensions.Storage.Blob.Processors;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors;

public sealed class SetStorageBlobTierProcessor_Orchestrator : StorageDataPlaneProcessorBase, IBlobCliProcessor
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

    public string BlobType { get; set; } = "block";

    internal static readonly string[] BlockTiers = ["Hot", "Cool", "Cold", "Archive"];
    internal static readonly string[] PageTiers = ["P4", "P6", "P10", "P15", "P20", "P30", "P40", "P50", "P60", "P70", "P80"];
    public string Tier { get; set; } = string.Empty;
    public string? RehydratePriority { get; set; }
    internal string? ExpectedBlobType { get; set; }
    internal Func<string, string, bool> ShouldProcess { get; set; } = (_, _) => true;

    private Task ExecuteAsync()
    {
        ExpectedBlobType = BlobType switch
        {
            "block" => "BlockBlob",
            "page" => "PageBlob",
            _ => throw new ArgumentException("Specify blob type block or page.", nameof(BlobType))
        };
        if (!(BlobType == "block" ? BlockTiers : PageTiers).Contains(Tier, StringComparer.Ordinal) ||
            RehydratePriority is not null && (BlobType == "page" || RehydratePriority is not ("High" or "Standard")))
            throw new ArgumentException("The tier or rehydration priority does not apply to the selected blob type.");
        BlobCliOperations.ResolveTarget(this);
        return ExecuteOperationAsync();
    }

    internal override Task ProcessNativeAsync() => ProcessWorkflowAsync(ExecuteOperationAsync);

    private async Task ExecuteOperationAsync()
    {
        var target = BlobTargetOperations.Resolve(this, this);
        if (IfMatch is not null || IfNoneMatch is not null || IfModifiedSince is not null || IfUnmodifiedSince is not null)
            throw new ArgumentException("Setting a blob tier supports lease and tag conditions only.");
        if (!BlockTiers.Contains(Tier, StringComparer.Ordinal) && !PageTiers.Contains(Tier, StringComparer.Ordinal) ||
            RehydratePriority is not null && RehydratePriority is not ("High" or "Standard"))
            throw new ArgumentException("Specify a supported Block/Page tier and rehydration priority.");
        if (!ShouldProcess(target.Url, "Set blob tier")) return;
        var blob = await BlobTargetOperations.ReadPropertiesAsync(this, target);
        if (ExpectedBlobType is not null && ExpectedBlobType != blob.Properties.BlobType ||
            blob.Properties.BlobType is not ("BlockBlob" or "PageBlob") ||
            !(blob.Properties.BlobType == "BlockBlob" ? BlockTiers : PageTiers).Contains(Tier, StringComparer.Ordinal) ||
            blob.Properties.BlobType == "PageBlob" && RehydratePriority is not null)
            throw new ArgumentException("The requested tier or rehydration priority does not apply to this blob type.");
        var child = BlobContainerOperations.CreateProcessor<BlobProcessors.SetTierBlobProcessor>(this, target.Container, processor =>
        {
            processor.Url = target.Url; processor.Timeout = Timeout; processor.Tier = Tier;
            processor.RehydratePriority = RehydratePriority; processor.LeaseId = LeaseId; processor.IfTags = IfTags;
        });
        using var response = await child.SendAsync();
    }
}
