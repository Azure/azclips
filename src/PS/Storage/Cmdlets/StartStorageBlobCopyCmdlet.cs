// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Storage;

[OutputType("Microsoft.Azure.AzClips.Extensions.StorageBlob.Models.BlobCopyResult")]
[Cmdlet(VerbsLifecycle.Start, "AzStorageBlobCopy")]
public class StartStorageBlobCopyCmdlet : PSCmdlet
{
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(Mandatory = false)]
    public string? Url { get; set; }

    [Parameter(Mandatory = false)]
    public string? StorageAccountName { get; set; }

    [Parameter(Mandatory = false)]
    public string? StorageAccountKey { get; set; }

    [Parameter(Mandatory = false)]
    public string? SasToken { get; set; }

    [Parameter(Mandatory = false)]
    public string? ContainerName { get; set; }

    [Parameter(Mandatory = false)]
    public string? BlobName { get; set; }

    [Parameter(Mandatory = true)]
    public string SourceUrl { get; set; } = "";

    [Parameter(Mandatory = false)]
    public string[]? Metadata { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? Tags { get; set; }

    [Parameter(Mandatory = false)]
    public string? Tier { get; set; }

    [Parameter(Mandatory = false)]
    [ValidateSet("High", "Standard", IgnoreCase = true)]
    public string? RehydratePriority { get; set; }

    [Parameter(Mandatory = false)]
    public string? LeaseId { get; set; }

    [Parameter(Mandatory = false)]
    public string? SourceIfModifiedSince { get; set; }

    [Parameter(Mandatory = false)]
    public string? SourceIfUnmodifiedSince { get; set; }

    [Parameter(Mandatory = false)]
    public string? SourceIfMatch { get; set; }

    [Parameter(Mandatory = false)]
    public string? SourceIfNoneMatch { get; set; }

    [Parameter(Mandatory = false)]
    public string? SourceIfTags { get; set; }

    [Parameter(Mandatory = false)]
    public string? IfModifiedSince { get; set; }

    [Parameter(Mandatory = false)]
    public string? IfUnmodifiedSince { get; set; }

    [Parameter(Mandatory = false)]
    public string? IfMatch { get; set; }

    [Parameter(Mandatory = false)]
    public string? IfNoneMatch { get; set; }

    [Parameter(Mandatory = false)]
    public string? IfTags { get; set; }

    [Parameter(Mandatory = false)]
    public int Timeout { get; set; }

    [Parameter(Mandatory = false)]
    public string? ClientRequestId { get; set; }

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
        if (MyInvocation.BoundParameters.ContainsKey(nameof(StorageAccountKey))) { parameters.Add("StorageAccountKey", StorageAccountKey!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(SasToken))) { parameters.Add("SasToken", SasToken!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ContainerName))) { parameters.Add("ContainerName", ContainerName!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(BlobName))) { parameters.Add("BlobName", BlobName!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(SourceUrl))) { parameters.Add("SourceUrl", SourceUrl); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Metadata))) { parameters.Add("Metadata", Metadata!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Tags))) { parameters.Add("Tags", Tags!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Tier))) { parameters.Add("Tier", Tier!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(RehydratePriority))) { parameters.Add("RehydratePriority", RehydratePriority!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(LeaseId))) { parameters.Add("LeaseId", LeaseId!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(SourceIfModifiedSince))) { parameters.Add("SourceIfModifiedSince", SourceIfModifiedSince!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(SourceIfUnmodifiedSince))) { parameters.Add("SourceIfUnmodifiedSince", SourceIfUnmodifiedSince!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(SourceIfMatch))) { parameters.Add("SourceIfMatch", SourceIfMatch!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(SourceIfNoneMatch))) { parameters.Add("SourceIfNoneMatch", SourceIfNoneMatch!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(SourceIfTags))) { parameters.Add("SourceIfTags", SourceIfTags!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(IfModifiedSince))) { parameters.Add("IfModifiedSince", IfModifiedSince!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(IfUnmodifiedSince))) { parameters.Add("IfUnmodifiedSince", IfUnmodifiedSince!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(IfMatch))) { parameters.Add("IfMatch", IfMatch!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(IfNoneMatch))) { parameters.Add("IfNoneMatch", IfNoneMatch!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(IfTags))) { parameters.Add("IfTags", IfTags!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Timeout))) { parameters.Add("Timeout", Timeout); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ClientRequestId))) { parameters.Add("ClientRequestId", ClientRequestId!); }

        var moduleName = MyInvocation.MyCommand.ModuleName;
        var moduleVersion = MyInvocation.MyCommand.Version?.ToString();
        var invocation = new CommandInvocation("Storage", "Start-AzStorageBlobCopy", parameters, false, moduleName, moduleVersion);
        telemetryService.TagInvocation(invocation);

        using ISession session = engine.OpenExecutionSession(invocation, streams, _cts.Token);
        session.Run();
    }

    protected override void EndProcessing() => _cts.Dispose();

    protected override void StopProcessing() => _cts.Cancel();
}
