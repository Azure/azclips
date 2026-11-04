// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Management.Automation;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;
using Microsoft.Azure.Azclips.Extensions.Storage.Processors;

namespace Microsoft.Azure.Azclips.Cmdlets.Storage;

[Cmdlet(VerbsLifecycle.Start, "AzStorageBlobCopy", DefaultParameterSetName = "NamedBlob", SupportsShouldProcess = true)]
[OutputType(typeof(StorageBlob))]
public sealed class StartAzStorageBlobCopyCmdlet : BlobContentCmdletBase
{
    [Parameter(Mandatory = true, ParameterSetName = "NamedBlob")]
    [Alias("SourceContainer")] public string? SrcContainer { get; set; }
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = "NamedBlob")]
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = "NativeContainer")]
    [Alias("SourceBlob")] public string? SrcBlob { get; set; }
    [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = "NativeBlob")]
    [Alias("CloudBlob", "SrcCloudBlob", "SourceCloudBlob", "ICloudBlob", "SrcICloudBlob", "SourceICloudBlob")]
    public StorageBlob? InputObject { get; set; }
    [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = "NativeContainer")]
    [Alias("CloudBlobContainer", "SourceCloudBlobContainer")]
    public StorageBlobContainer? ContainerObject { get; set; }
    [Parameter(Mandatory = true, ParameterSetName = "Uri")]
    [Alias("SrcUri", "SourceUri")] public string? AbsoluteUri { get; set; }
    [Parameter(Mandatory = true, ParameterSetName = "NamedFile")]
    [Alias("SourceShareName")] public string? SrcShareName { get; set; }
    [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = "NativeShare")]
    [Alias("SourceShare")] public StorageFileShare? SrcShare { get; set; }
    [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = "NativeDirectory")]
    [Alias("SourceDir")] public StorageFileDirectory? SrcDir { get; set; }
    [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = "NativeFile")]
    [Alias("SourceFile")] public StorageFile? SrcFile { get; set; }
    [Parameter(Mandatory = true, ParameterSetName = "NamedFile")]
    [Parameter(Mandatory = true, ParameterSetName = "NativeShare")]
    [Parameter(Mandatory = true, ParameterSetName = "NativeDirectory")]
    [Alias("SourceFilePath")] public string? SrcFilePath { get; set; }

    [Parameter] [Alias("DestinationContainer")] public string? DestContainer { get; set; }
    [Parameter] [Alias("DestinationBlob")] public string? DestBlob { get; set; }
    [Parameter] [Alias("DestinationContainerObject")] public StorageBlobContainer? DestContainerObject { get; set; }
    [Parameter] [Alias("DestCloudBlob", "DestinationCloudBlob", "DestICloudBlob", "DestinationICloudBlob")]
    public StorageBlob? DestBlobObject { get; set; }
    [Parameter] [Alias("DestinationContext")] public StorageContext? DestContext { get; set; }
    [Parameter] [Alias("SourceContext")] public StorageContext? SrcContext { get; set; }
    [Parameter] public DateTimeOffset? SnapshotTime { get; set; }
    [Parameter] public string? VersionId { get; set; }
    [Parameter] public string? SourceShareSnapshot { get; set; }
    [Parameter] public string? SourceSas { get; set; }
    [Parameter] public string? SourceAccountKey { get; set; }
    [Parameter] public SwitchParameter UseSourceAccountKeyLookup { get; set; }
    [Parameter] public string? SourceKeyResourceGroup { get; set; }
    [Parameter] public string? SourceKeySubscriptionId { get; set; }
    [Parameter] public string SourceKeyName { get; set; } = "key1";
    [Parameter] public SwitchParameter EnableSourceFileBackupRequestIntent { get; set; }
    [Parameter] public string? SourceIfMatch { get; set; }
    [Parameter] public string? SourceIfNoneMatch { get; set; }
    [Parameter] public DateTimeOffset? SourceIfModifiedSince { get; set; }
    [Parameter] public DateTimeOffset? SourceIfUnmodifiedSince { get; set; }
    [Parameter] public string? SourceTagCondition { get; set; }
    [Parameter] public System.Collections.Hashtable? Metadata { get; set; }
    [Parameter] [Alias("Tags")] public System.Collections.Hashtable? Tag { get; set; }
    [Parameter] [ValidateSet("Hot", "Cool", "Cold", "Archive")] public string? StandardBlobTier { get; set; }
    [Parameter] [ValidateSet("P4", "P6", "P10", "P15", "P20", "P30", "P40", "P50", "P60", "P70", "P80")]
    public string? PremiumPageBlobTier { get; set; }
    [Parameter] [ValidateSet("High", "Standard")] public string? RehydratePriority { get; set; }

    protected override void ProcessRecord()
    {
        if (StandardBlobTier is not null && PremiumPageBlobTier is not null)
            throw new ArgumentException("Specify a standard Block tier or a premium Page tier, not both.");
        var request = new StartStorageBlobCopyProcessor_Orchestrator
        {
            Container = DestContainer, Blob = DestBlob, ContainerObject = DestContainerObject, InputObject = DestBlobObject,
            SourceContext = SrcContext ?? Context,
            SourceContainer = SrcContainer, SourceBlob = SrcBlob, SourceBlobObject = InputObject,
            SourceContainerObject = ContainerObject, SourceUri = AbsoluteUri,
            SourceShareName = SrcShareName, SourceShareObject = SrcShare, SourceDirectoryObject = SrcDir,
            SourceFileObject = SrcFile, SourcePath = SrcFilePath,
            SourceSnapshot = SnapshotTime?.UtcDateTime.ToString("O", CultureInfo.InvariantCulture), SourceVersionId = VersionId,
            SourceShareSnapshot = SourceShareSnapshot,
            SourceSas = SourceSas, SourceAccountKey = SourceAccountKey,
            UseSourceAccountKeyLookup = UseSourceAccountKeyLookup.IsPresent, SourceKeyResourceGroup = SourceKeyResourceGroup,
            SourceKeySubscriptionId = SourceKeySubscriptionId, SourceKeyName = SourceKeyName,
            EnableSourceFileBackupRequestIntent = EnableSourceFileBackupRequestIntent.IsPresent,
            SourceIfMatch = SourceIfMatch, SourceIfNoneMatch = SourceIfNoneMatch,
            SourceIfModifiedSince = SourceIfModifiedSince, SourceIfUnmodifiedSince = SourceIfUnmodifiedSince,
            SourceIfTags = SourceTagCondition, Metadata = Strings(Metadata), Tags = Strings(Tag),
            Tier = StandardBlobTier is null ? PremiumPageBlobTier?.ToUpperInvariant() :
                CultureInfo.InvariantCulture.TextInfo.ToTitleCase(StandardBlobTier.ToLowerInvariant()),
            RehydratePriority = RehydratePriority is null ? null :
                CultureInfo.InvariantCulture.TextInfo.ToTitleCase(RehydratePriority.ToLowerInvariant())
        };
        ConfigureTarget(request);
        request.Context = DestContext ?? DestBlobObject?.Context ?? DestContainerObject?.Context ?? Context ??
            InputObject?.Context ?? ContainerObject?.Context ?? SrcFile?.Context ?? SrcDir?.Context ?? SrcShare?.Context ?? SrcContext;
        var command = new StartStorageBlobCopyPowerShellProcessor_Orchestrator { Request = request };
        ExecuteWrite("Start-AzStorageBlobCopy", command.Prepare(Force.IsPresent));
    }
}
