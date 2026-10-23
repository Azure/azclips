// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

public sealed class StartStorageBlobCopyCommand : BlobCliCommandBase
{
    public string? SourceUri { get; set; }
    public string? SourceSas { get; set; }
    public string? SourceAccountKey { get; set; }
    public bool UseSourceAccountKeyLookup { get; set; }
    public string? SourceKeyResourceGroup { get; set; }
    public string? SourceKeySubscriptionId { get; set; }
    public string SourceKeyName { get; set; } = "key1";
    public string? SourceStorageAccountName { get; set; }
    public string? SourceContainer { get; set; }
    public string? SourceBlob { get; set; }
    public string? SourceSnapshot { get; set; }
    public string? SourceVersionId { get; set; }
    public string? SourceShareName { get; set; }
    public string? SourcePath { get; set; }
    public string? SourceShareSnapshot { get; set; }
    public bool EnableSourceFileBackupRequestIntent { get; set; }
    public string? SourceIfMatch { get; set; }
    public string? SourceIfNoneMatch { get; set; }
    public string? SourceIfModifiedSinceInput { get; set; }
    public string? SourceIfUnmodifiedSinceInput { get; set; }
    public string? SourceIfTags { get; set; }
    public string[]? MetadataInput { get; set; }
    public string[]? TagsInput { get; set; }
    public string? Tier { get; set; }
    public string? RehydratePriority { get; set; }

    protected override Task ExecuteAsync()
    {
        var metadata = StorageCliKeyValue.Parse(MetadataInput, tags: false);
        var tags = StorageCliKeyValue.Parse(TagsInput, tags: true);
        var sourceModified = ParseDate(SourceIfModifiedSinceInput);
        var sourceUnmodified = ParseDate(SourceIfUnmodifiedSinceInput);
        var child = CreateTargetWorkflow<StartStorageBlobCopyProcessor>(processor =>
        {
            processor.SourceUri = SourceUri;
            processor.SourceSas = SourceSas;
            processor.SourceAccountKey = SourceAccountKey;
            processor.UseSourceAccountKeyLookup = UseSourceAccountKeyLookup;
            processor.SourceKeyResourceGroup = SourceKeyResourceGroup;
            processor.SourceKeySubscriptionId = SourceKeySubscriptionId;
            processor.SourceKeyName = SourceKeyName;
            processor.SourceStorageAccountName = SourceStorageAccountName;
            processor.SourceContainer = SourceContainer;
            processor.SourceBlob = SourceBlob;
            processor.SourceShareName = SourceShareName;
            processor.SourcePath = SourcePath;
            processor.SourceSnapshot = SourceSnapshot;
            processor.SourceVersionId = SourceVersionId;
            processor.SourceShareSnapshot = SourceShareSnapshot;
            processor.EnableSourceFileBackupRequestIntent = EnableSourceFileBackupRequestIntent;
            processor.SourceIfMatch = SourceIfMatch;
            processor.SourceIfNoneMatch = SourceIfNoneMatch;
            processor.SourceIfModifiedSince = sourceModified;
            processor.SourceIfUnmodifiedSince = sourceUnmodified;
            processor.SourceIfTags = SourceIfTags;
            processor.Metadata = metadata;
            processor.Tags = tags;
            processor.Tier = Tier;
            processor.RehydratePriority = RehydratePriority;
            processor.Overwrite = true;
            processor.ReturnCopyState = true;
            processor.CopyStateHandler = (result, token) => WriteOutput(StorageBlobCliOutput.ProjectCopyStart(result), token);
        });
        return child.ProcessAsync();
    }

}
