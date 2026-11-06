// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Management.Automation;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;

namespace Microsoft.Azure.Azclips.Cmdlets.Storage;

[Cmdlet(VerbsCommon.Get, "AzStorageBlob", DefaultParameterSetName = BlobNameSet)]
[OutputType(typeof(StorageBlob))]
public sealed class GetAzStorageBlobCmdlet : StorageCmdletBase
{
    private const string BlobNameSet = "BlobName";
    private const string BlobPrefixSet = "BlobPrefix";
    private const string SnapshotSet = "SingleBlobSnapshotTime";
    private const string VersionSet = "SingleBlobVersionID";
    private const string ContainerNameSet = "ContainerBlobName";
    private const string ContainerPrefixSet = "ContainerBlobPrefix";
    private const string ContainerSnapshotSet = "ContainerBlobSnapshotTime";
    private const string ContainerVersionSet = "ContainerBlobVersionID";
    private const string BlobObjectSet = "BlobObject";

    [Parameter(Mandatory = true, ParameterSetName = BlobNameSet)]
    [Parameter(Mandatory = true, ParameterSetName = BlobPrefixSet)]
    [Parameter(Mandatory = true, ParameterSetName = SnapshotSet)]
    [Parameter(Mandatory = true, ParameterSetName = VersionSet)]
    [Parameter(ParameterSetName = ContainerNameSet)]
    [Parameter(ParameterSetName = ContainerPrefixSet)]
    [Parameter(ParameterSetName = ContainerSnapshotSet)]
    [Parameter(ParameterSetName = ContainerVersionSet)]
    [Parameter(ParameterSetName = BlobObjectSet)]
    [ValidateNotNull]
    public StorageContext? Context { get; set; }

    [Parameter(Mandatory = true, Position = 1, ValueFromPipelineByPropertyName = true, ParameterSetName = BlobNameSet)]
    [Parameter(Mandatory = true, Position = 1, ValueFromPipelineByPropertyName = true, ParameterSetName = BlobPrefixSet)]
    [Parameter(Mandatory = true, Position = 1, ValueFromPipelineByPropertyName = true, ParameterSetName = SnapshotSet)]
    [Parameter(Mandatory = true, Position = 1, ValueFromPipelineByPropertyName = true, ParameterSetName = VersionSet)]
    [Alias("N", "Name")]
    [ValidateNotNullOrEmpty]
    public string? Container { get; set; }

    [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = ContainerNameSet)]
    [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = ContainerPrefixSet)]
    [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = ContainerSnapshotSet)]
    [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = ContainerVersionSet)]
    [ValidateNotNull]
    public StorageBlobContainer? ContainerObject { get; set; }

    [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = BlobObjectSet)]
    [ValidateNotNull]
    public StorageBlob? InputObject { get; set; }

    [Parameter(Position = 0, ParameterSetName = BlobNameSet)]
    [Parameter(Position = 0, ParameterSetName = ContainerNameSet)]
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = SnapshotSet)]
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = VersionSet)]
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = ContainerSnapshotSet)]
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = ContainerVersionSet)]
    [SupportsWildcards]
    [AllowEmptyString]
    public string? Blob { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = BlobPrefixSet)]
    [Parameter(Mandatory = true, ParameterSetName = ContainerPrefixSet)]
    [AllowEmptyString]
    public string? Prefix { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = SnapshotSet)]
    [Parameter(Mandatory = true, ParameterSetName = ContainerSnapshotSet)]
    public DateTimeOffset SnapshotTime { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = VersionSet)]
    [Parameter(Mandatory = true, ParameterSetName = ContainerVersionSet)]
    [ValidateNotNullOrEmpty]
    public string? VersionId { get; set; }

    [Parameter(ParameterSetName = BlobNameSet)]
    [Parameter(ParameterSetName = BlobPrefixSet)]
    [Parameter(ParameterSetName = ContainerNameSet)]
    [Parameter(ParameterSetName = ContainerPrefixSet)]
    public SwitchParameter IncludeDeleted { get; set; }

    [Parameter(ParameterSetName = BlobPrefixSet)]
    [Parameter(ParameterSetName = ContainerPrefixSet)]
    public SwitchParameter IncludeVersion { get; set; }

    [Parameter]
    public SwitchParameter IncludeTag { get; set; }

    [Parameter(ParameterSetName = BlobNameSet)]
    [Parameter(ParameterSetName = BlobPrefixSet)]
    [Parameter(ParameterSetName = ContainerNameSet)]
    [Parameter(ParameterSetName = ContainerPrefixSet)]
    public int MaxCount { get; set; } = int.MaxValue;

    [Parameter(ParameterSetName = BlobNameSet)]
    [Parameter(ParameterSetName = BlobPrefixSet)]
    [Parameter(ParameterSetName = ContainerNameSet)]
    [Parameter(ParameterSetName = ContainerPrefixSet)]
    [ValidateNotNull]
    public StorageBlobContinuationToken? ContinuationToken { get; set; }

    [Parameter(ParameterSetName = BlobNameSet)]
    [Parameter(ParameterSetName = SnapshotSet)]
    [Parameter(ParameterSetName = VersionSet)]
    [Parameter(ParameterSetName = ContainerNameSet)]
    [Parameter(ParameterSetName = ContainerSnapshotSet)]
    [Parameter(ParameterSetName = ContainerVersionSet)]
    [Parameter(ParameterSetName = BlobObjectSet)]
    [ValidateNotNullOrEmpty]
    public string? TagCondition { get; set; }

    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int? ServerTimeoutPerRequest { get; set; }

    protected override void ProcessRecord()
    {
        bool selected = ParameterSetName is SnapshotSet or VersionSet or ContainerSnapshotSet or ContainerVersionSet or BlobObjectSet;
        bool list = !selected && (Prefix is not null || string.IsNullOrEmpty(Blob) ||
            WildcardPattern.ContainsWildcardCharacters(Blob) || IncludeDeleted.IsPresent);
        if (list && TagCondition is not null)
            throw new PSArgumentException("TagCondition requires an exact blob lookup.");
        if (selected && InputObject is null && string.IsNullOrEmpty(Blob))
            throw new PSArgumentException("A snapshot or version requires a nonempty exact blob name.");

        var parameters = new Dictionary<string, object> { ["ListMode"] = list };
        if (Context is not null) parameters.Add(nameof(Context), Context);
        if (Container is not null) parameters.Add(nameof(Container), Container);
        if (ContainerObject is not null) parameters.Add(nameof(ContainerObject), ContainerObject);
        if (InputObject is not null) parameters.Add(nameof(InputObject), InputObject);
        if (list)
        {
            if (Prefix is null && !string.IsNullOrEmpty(Blob))
            {
                // Azure prefixes are case-sensitive, so wildcard matching must stay client-side.
                var wildcard = new WildcardPattern(Blob, WildcardOptions.IgnoreCase | WildcardOptions.Compiled);
                parameters.Add("NameFilter", new Func<string, bool>(wildcard.IsMatch));
            }
            parameters.Add(nameof(Prefix), Prefix ?? string.Empty);
            if (MyInvocation.BoundParameters.ContainsKey(nameof(MaxCount))) parameters.Add(nameof(MaxCount), MaxCount);
            if (ContinuationToken is not null) parameters.Add(nameof(ContinuationToken), ContinuationToken);
            if (MyInvocation.BoundParameters.ContainsKey(nameof(IncludeVersion)))
                parameters.Add(nameof(IncludeVersion), IncludeVersion.IsPresent);
        }
        else
        {
            if (Blob is not null) parameters.Add(nameof(Blob), Blob);
            if (ParameterSetName is SnapshotSet or ContainerSnapshotSet)
                parameters.Add("Snapshot", SnapshotTime.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
            if (VersionId is not null) parameters.Add(nameof(VersionId), VersionId);
            if (TagCondition is not null) parameters.Add("IfTags", TagCondition);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(IncludeDeleted))) parameters.Add(nameof(IncludeDeleted), IncludeDeleted.IsPresent);
        if (MyInvocation.BoundParameters.ContainsKey(nameof(IncludeTag))) parameters.Add(nameof(IncludeTag), IncludeTag.IsPresent);
        if (ServerTimeoutPerRequest is not null) parameters.Add("Timeout", ServerTimeoutPerRequest.Value);
        Execute("Get-AzStorageBlob", parameters);
    }
}
