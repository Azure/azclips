// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Storage;

[Cmdlet(VerbsCommon.New, "AzStorageContainer")]
public class NewStorageContainerCmdlet : PSCmdlet
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
    public string[]? Metadata { get; set; }

    [Parameter(Mandatory = false)]
    [ValidateSet("blob", "container", IgnoreCase = true)]
    public string? PublicAccess { get; set; }

    [Parameter(Mandatory = false)]
    public string? DefaultEncryptionScope { get; set; }

    [Parameter(Mandatory = false)]
    public SwitchParameter PreventEncryptionScopeOverride { get; set; }

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
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Metadata))) { parameters.Add("Metadata", Metadata!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(PublicAccess))) { parameters.Add("PublicAccess", PublicAccess!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(DefaultEncryptionScope))) { parameters.Add("DefaultEncryptionScope", DefaultEncryptionScope!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(PreventEncryptionScopeOverride))) { parameters.Add("PreventEncryptionScopeOverride", PreventEncryptionScopeOverride.IsPresent); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Timeout))) { parameters.Add("Timeout", Timeout); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ClientRequestId))) { parameters.Add("ClientRequestId", ClientRequestId!); }

        var moduleName = MyInvocation.MyCommand.ModuleName;
        var moduleVersion = MyInvocation.MyCommand.Version?.ToString();
        var invocation = new CommandInvocation("Storage", "New-AzStorageContainer", parameters, false, moduleName, moduleVersion);
        telemetryService.TagInvocation(invocation);

        using ISession session = engine.OpenExecutionSession(invocation, streams, _cts.Token);
        session.Run();
    }

    protected override void EndProcessing() => _cts.Dispose();

    protected override void StopProcessing() => _cts.Cancel();
}
