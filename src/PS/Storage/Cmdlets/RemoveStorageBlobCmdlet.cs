// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Management.Automation;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;

namespace Microsoft.Azure.Azclips.Cmdlets.Storage;

[Cmdlet(VerbsCommon.Remove, "AzStorageBlob", DefaultParameterSetName = NameSet, SupportsShouldProcess = true)]
[OutputType(typeof(bool))]
public sealed class RemoveAzStorageBlobCmdlet : StorageCmdletBase
{
    private const string NameSet = "NamePipeline";
    private const string BlobSet = "BlobPipeline";
    private const string ContainerSet = "ContainerPipeline";

    [Parameter(Mandatory = true, ParameterSetName = NameSet)]
    [Parameter(ParameterSetName = BlobSet)]
    [Parameter(ParameterSetName = ContainerSet)]
    [ValidateNotNull]
    public StorageContext? Context { get; set; }

    [Parameter(Mandatory = true, Position = 0, ParameterSetName = NameSet)]
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = ContainerSet)]
    [ValidateNotNullOrEmpty]
    public string? Blob { get; set; }

    [Parameter(Mandatory = true, Position = 1, ParameterSetName = NameSet)]
    [ValidateNotNullOrEmpty]
    public string? Container { get; set; }

    [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = BlobSet)]
    [ValidateNotNull]
    public StorageBlob? InputObject { get; set; }

    [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = ContainerSet)]
    [ValidateNotNull]
    public StorageBlobContainer? ContainerObject { get; set; }

    [Parameter(ParameterSetName = NameSet)]
    [Parameter(ParameterSetName = ContainerSet)]
    [ValidateNotNull]
    public DateTimeOffset? SnapshotTime { get; set; }

    [Parameter(ParameterSetName = NameSet)]
    [Parameter(ParameterSetName = ContainerSet)]
    [ValidateNotNullOrEmpty]
    public string? VersionId { get; set; }

    [Parameter]
    public SwitchParameter DeleteSnapshot { get; set; }

    [Parameter]
    public SwitchParameter Force { get; set; }

    [Parameter]
    public SwitchParameter PassThru { get; set; }

    [Parameter]
    [ValidateNotNullOrEmpty]
    public string? TagCondition { get; set; }

    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int? ServerTimeoutPerRequest { get; set; }

    protected override void ProcessRecord()
    {
        bool selected = SnapshotTime is not null || VersionId is not null ||
            InputObject?.Snapshot is not null || InputObject?.VersionId is not null;
        string? deletion = DeleteSnapshot.IsPresent ? "only" : Force.IsPresent && !selected ? "include" : null;
        string? snapshot = SnapshotTime?.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
        var preflight = new RemoveStorageBlobPowerShellCommand
        {
            Context = Context, Container = Container, Blob = Blob, InputObject = InputObject,
            ContainerObject = ContainerObject, Snapshot = snapshot, VersionId = VersionId,
            DeleteSnapshots = deletion, Profile = Profile, Timeout = ServerTimeoutPerRequest
        };
        string target = preflight.GetConfirmationTarget();
        string action = deletion switch
        {
            "only" => "Delete blob snapshots only",
            "include" => "Delete blob and snapshots",
            _ => "Delete blob"
        };
        if (!ShouldProcess(target, action)) return;

        var parameters = new Dictionary<string, object>();
        if (Context is not null) parameters.Add(nameof(Context), Context);
        if (Container is not null) parameters.Add(nameof(Container), Container);
        if (Blob is not null) parameters.Add(nameof(Blob), Blob);
        if (InputObject is not null) parameters.Add(nameof(InputObject), InputObject);
        if (ContainerObject is not null) parameters.Add(nameof(ContainerObject), ContainerObject);
        if (snapshot is not null) parameters.Add("Snapshot", snapshot);
        if (VersionId is not null) parameters.Add(nameof(VersionId), VersionId);
        if (deletion is not null) parameters.Add("DeleteSnapshots", deletion);
        if (MyInvocation.BoundParameters.ContainsKey(nameof(PassThru))) parameters.Add(nameof(PassThru), PassThru.IsPresent);
        if (TagCondition is not null) parameters.Add("IfTags", TagCondition);
        if (ServerTimeoutPerRequest is not null) parameters.Add("Timeout", ServerTimeoutPerRequest.Value);
        try
        {
            Execute("Remove-AzStorageBlob", parameters);
        }
        catch (StorageBlobSnapshotsPresentException conflict)
        {
            StoppingToken.ThrowIfCancellationRequested();
            if (!ShouldContinue($"Delete '{conflict.BlobUri.AbsoluteUri}' and all its snapshots?", "Blob snapshot removal"))
            {
                WriteVerbose("Blob removal cancelled; snapshots were not included.");
                return;
            }
            StoppingToken.ThrowIfCancellationRequested();
            var retry = new Dictionary<string, object>(parameters)
            {
                [nameof(Context)] = conflict.Container.Context,
                [nameof(ContainerObject)] = conflict.Container,
                [nameof(Blob)] = conflict.BlobName,
                ["DeleteSnapshots"] = "include"
            };
            retry.Remove(nameof(Container));
            retry.Remove(nameof(InputObject));
            retry.Remove(nameof(Profile));
            Execute("Remove-AzStorageBlob", retry);
        }
    }
}
