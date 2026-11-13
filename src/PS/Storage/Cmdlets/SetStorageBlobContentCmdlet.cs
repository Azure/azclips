// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Storage;

[OutputType("Microsoft.Azure.AzClips.Extensions.StorageBlob.Models.BlobUploadResult")]
[Cmdlet(VerbsCommon.Set, "AzStorageBlobContent")]
public class SetStorageBlobContentCmdlet : PSCmdlet
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
    public string FilePath { get; set; } = "";

    [Parameter(Mandatory = false)]
    public string[]? Metadata { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? Tags { get; set; }

    [Parameter(Mandatory = false)]
    public string? BlobContentType { get; set; }

    [Parameter(Mandatory = false)]
    public string? BlobContentEncoding { get; set; }

    [Parameter(Mandatory = false)]
    public string? BlobContentLanguage { get; set; }

    [Parameter(Mandatory = false)]
    public string? BlobContentDisposition { get; set; }

    [Parameter(Mandatory = false)]
    public string? BlobCacheControl { get; set; }

    [Parameter(Mandatory = false)]
    public string? Tier { get; set; }

    [Parameter(Mandatory = false)]
    public string? EncryptionScope { get; set; }

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
        if (MyInvocation.BoundParameters.ContainsKey(nameof(FilePath))) { parameters.Add("FilePath", FilePath); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Metadata))) { parameters.Add("Metadata", Metadata!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Tags))) { parameters.Add("Tags", Tags!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(BlobContentType))) { parameters.Add("BlobContentType", BlobContentType!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(BlobContentEncoding))) { parameters.Add("BlobContentEncoding", BlobContentEncoding!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(BlobContentLanguage))) { parameters.Add("BlobContentLanguage", BlobContentLanguage!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(BlobContentDisposition))) { parameters.Add("BlobContentDisposition", BlobContentDisposition!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(BlobCacheControl))) { parameters.Add("BlobCacheControl", BlobCacheControl!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Tier))) { parameters.Add("Tier", Tier!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(EncryptionScope))) { parameters.Add("EncryptionScope", EncryptionScope!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Timeout))) { parameters.Add("Timeout", Timeout); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ClientRequestId))) { parameters.Add("ClientRequestId", ClientRequestId!); }

        var moduleName = MyInvocation.MyCommand.ModuleName;
        var moduleVersion = MyInvocation.MyCommand.Version?.ToString();
        var invocation = new CommandInvocation("Storage", "Set-AzStorageBlobContent", parameters, false, moduleName, moduleVersion);
        telemetryService.TagInvocation(invocation);

        using ISession session = engine.OpenExecutionSession(invocation, streams, _cts.Token);
        session.Run();
    }

    protected override void EndProcessing() => _cts.Dispose();

    protected override void StopProcessing() => _cts.Cancel();
}
