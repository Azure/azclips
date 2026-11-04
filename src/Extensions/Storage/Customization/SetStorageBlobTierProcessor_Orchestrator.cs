// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;
using BlobModels = Microsoft.Azure.Azclips.Extensions.Storage.Blob.Models;
using BlobProcessors = Microsoft.Azure.Azclips.Extensions.Storage.Blob.Processors;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors;

public sealed class SetStorageBlobTierProcessor_Orchestrator : BlobCliOrchestratorBase
{
    public string BlobType { get; set; } = "block";

    internal static readonly string[] BlockTiers = ["Hot", "Cool", "Cold", "Archive"];
    internal static readonly string[] PageTiers = ["P4", "P6", "P10", "P15", "P20", "P30", "P40", "P50", "P60", "P70", "P80"];
    public string Tier { get; set; } = string.Empty;
    public string? RehydratePriority { get; set; }
    internal string? ExpectedBlobType { get; set; }
    internal Func<string, string, bool> ShouldProcess { get; set; } = (_, _) => true;

    protected override Task ExecuteAsync()
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
        ResolveCliTarget();
        return ExecuteOperationAsync();
    }

    internal override Task ProcessNativeAsync() => ProcessWorkflowAsync(ExecuteOperationAsync);

    private async Task ExecuteOperationAsync()
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
