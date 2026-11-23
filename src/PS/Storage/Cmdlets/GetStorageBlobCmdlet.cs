// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Storage;

[OutputType("Microsoft.Azure.AzClips.Extensions.StorageBlob.Models.BlobItem")]
[Cmdlet(VerbsCommon.Get, "AzStorageBlob", DefaultParameterSetName = ListParameterSet)]
public class GetStorageBlobCmdlet : PSCmdlet
{
    // The parameter set picks the extension index entry: Get-AzStorageBlob_{ParameterSetName}.
    private const string ListParameterSet = "List";
    private const string GetParameterSet = "Get";

    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(Mandatory = false)]
    public string? Url { get; set; }

    [Parameter(Mandatory = false)]
    public string? StorageAccountName { get; set; }

    [Parameter(Mandatory = false)]
    public string? ContainerName { get; set; }

    [Parameter(Mandatory = false)]
    public string? StorageAccountKey { get; set; }

    [Parameter(Mandatory = false)]
    public string? SasToken { get; set; }

    [Parameter(ParameterSetName = ListParameterSet, Mandatory = false)]
    public string? Prefix { get; set; }

    [Parameter(ParameterSetName = ListParameterSet, Mandatory = false)]
    public string? Marker { get; set; }

    [Parameter(ParameterSetName = ListParameterSet, Mandatory = false)]
    public int Maxresults { get; set; }

    [Parameter(Mandatory = false)]
    public int Timeout { get; set; }

    [Parameter(ParameterSetName = ListParameterSet, Mandatory = false)]
    public string? StartFrom { get; set; }

    [Parameter(Mandatory = false)]
    public string? ClientRequestId { get; set; }

    [Parameter(ParameterSetName = GetParameterSet, Mandatory = false)]
    public string? BlobName { get; set; }

    [Parameter(ParameterSetName = GetParameterSet, Mandatory = false)]
    public string? Snapshot { get; set; }

    [Parameter(ParameterSetName = GetParameterSet, Mandatory = false)]
    public string? VersionId { get; set; }

    [Parameter(ParameterSetName = GetParameterSet, Mandatory = false)]
    public string? LeaseId { get; set; }

    [Parameter(ParameterSetName = GetParameterSet, Mandatory = false)]
    public string? EncryptionKey { get; set; }

    [Parameter(ParameterSetName = GetParameterSet, Mandatory = false)]
    public string? EncryptionKeySha256 { get; set; }

    [Parameter(ParameterSetName = GetParameterSet, Mandatory = false)]
    [ValidateSet("AES256", IgnoreCase = true)]
    public string? EncryptionAlgorithm { get; set; }

    [Parameter(ParameterSetName = GetParameterSet, Mandatory = false)]
    public string? IfModifiedSince { get; set; }

    [Parameter(ParameterSetName = GetParameterSet, Mandatory = false)]
    public string? IfUnmodifiedSince { get; set; }

    [Parameter(ParameterSetName = GetParameterSet, Mandatory = false)]
    public string? IfMatch { get; set; }

    [Parameter(ParameterSetName = GetParameterSet, Mandatory = false)]
    public string? IfNoneMatch { get; set; }

    [Parameter(ParameterSetName = GetParameterSet, Mandatory = false)]
    public string? IfTags { get; set; }

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "StorageError", ErrorCategory.NotSpecified, enumerateOutput: true);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetryService.StartRootActivity();

        var parameters = new Dictionary<string, object>();
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Url))) { parameters.Add("Url", Url!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(StorageAccountName))) { parameters.Add("StorageAccountName", StorageAccountName!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ContainerName))) { parameters.Add("ContainerName", ContainerName!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(StorageAccountKey))) { parameters.Add("StorageAccountKey", StorageAccountKey!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(SasToken))) { parameters.Add("SasToken", SasToken!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Prefix))) { parameters.Add("Prefix", Prefix!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Marker))) { parameters.Add("Marker", Marker!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Maxresults))) { parameters.Add("Maxresults", Maxresults); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Timeout))) { parameters.Add("Timeout", Timeout); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(StartFrom))) { parameters.Add("StartFrom", StartFrom!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ClientRequestId))) { parameters.Add("ClientRequestId", ClientRequestId!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(BlobName))) { parameters.Add("BlobName", BlobName!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Snapshot))) { parameters.Add("Snapshot", Snapshot!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(VersionId))) { parameters.Add("VersionId", VersionId!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(LeaseId))) { parameters.Add("LeaseId", LeaseId!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(EncryptionKey))) { parameters.Add("EncryptionKey", EncryptionKey!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(EncryptionKeySha256))) { parameters.Add("EncryptionKeySha256", EncryptionKeySha256!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(EncryptionAlgorithm))) { parameters.Add("EncryptionAlgorithm", EncryptionAlgorithm!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(IfModifiedSince))) { parameters.Add("IfModifiedSince", IfModifiedSince!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(IfUnmodifiedSince))) { parameters.Add("IfUnmodifiedSince", IfUnmodifiedSince!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(IfMatch))) { parameters.Add("IfMatch", IfMatch!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(IfNoneMatch))) { parameters.Add("IfNoneMatch", IfNoneMatch!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(IfTags))) { parameters.Add("IfTags", IfTags!); }

        var moduleName = MyInvocation.MyCommand.ModuleName;
        var moduleVersion = MyInvocation.MyCommand.Version?.ToString();
        var invocation = new CommandInvocation("Storage", $"Get-AzStorageBlob_{ParameterSetName}", parameters, false, moduleName, moduleVersion);
        telemetryService.TagInvocation(invocation);

        using ISession session = engine.OpenExecutionSession(invocation, streams, _cts.Token);
        session.Run();
    }

    protected override void EndProcessing() => _cts.Dispose();

    protected override void StopProcessing() => _cts.Cancel();
}
