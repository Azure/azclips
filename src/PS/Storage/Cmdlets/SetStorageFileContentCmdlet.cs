// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Storage;

[Cmdlet(VerbsCommon.Set, "AzStorageFileContent")]
public class SetStorageFileContentCmdlet : PSCmdlet
{
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(Mandatory = false)]
    public string? Url { get; set; }

    [Parameter(Mandatory = false)]
    public string? StorageAccountName { get; set; }

    [Parameter(Mandatory = false)]
    public string? ShareName { get; set; }

    [Parameter(Mandatory = false)]
    public string? FilePath { get; set; }

    [Parameter(Mandatory = false)]
    public string? StorageAccountKey { get; set; }

    [Parameter(Mandatory = false)]
    public string? SasToken { get; set; }

    [Parameter(Mandatory = false)]
    public string? Source { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? Metadata { get; set; }

    [Parameter(Mandatory = false)]
    public string? ContentType { get; set; }

    [Parameter(Mandatory = false)]
    public string? ContentEncoding { get; set; }

    [Parameter(Mandatory = false)]
    public string? ContentLanguage { get; set; }

    [Parameter(Mandatory = false)]
    public string? ContentDisposition { get; set; }

    [Parameter(Mandatory = false)]
    public string? CacheControl { get; set; }

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
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ShareName))) { parameters.Add("ShareName", ShareName!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(FilePath))) { parameters.Add("FilePath", FilePath!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(StorageAccountKey))) { parameters.Add("StorageAccountKey", StorageAccountKey!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(SasToken))) { parameters.Add("SasToken", SasToken!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Source))) { parameters.Add("Source", Source!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Metadata))) { parameters.Add("Metadata", Metadata!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ContentType))) { parameters.Add("ContentType", ContentType!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ContentEncoding))) { parameters.Add("ContentEncoding", ContentEncoding!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ContentLanguage))) { parameters.Add("ContentLanguage", ContentLanguage!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ContentDisposition))) { parameters.Add("ContentDisposition", ContentDisposition!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(CacheControl))) { parameters.Add("CacheControl", CacheControl!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Timeout))) { parameters.Add("Timeout", Timeout); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ClientRequestId))) { parameters.Add("ClientRequestId", ClientRequestId!); }

        var moduleName = MyInvocation.MyCommand.ModuleName;
        var moduleVersion = MyInvocation.MyCommand.Version?.ToString();
        var invocation = new CommandInvocation("Storage", "Set-AzStorageFileContent", parameters, false, moduleName, moduleVersion);
        telemetryService.TagInvocation(invocation);

        using ISession session = engine.OpenExecutionSession(invocation, streams, _cts.Token);
        session.Run();
    }

    protected override void EndProcessing() => _cts.Dispose();

    protected override void StopProcessing() => _cts.Cancel();
}
