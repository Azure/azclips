// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Storage;

[Cmdlet(VerbsCommon.Get, "AzStorageShare", DefaultParameterSetName = ListParameterSet)]
public class GetStorageShareCmdlet : PSCmdlet
{
    // The parameter set picks the extension index entry: Get-AzStorageShare_{ParameterSetName}.
    private const string ListParameterSet = "List";
    private const string GetParameterSet = "Get";

    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(Mandatory = false)]
    public string? Url { get; set; }

    [Parameter(Mandatory = false)]
    public string? StorageAccountName { get; set; }

    [Parameter(ParameterSetName = GetParameterSet, Mandatory = false)]
    public string? ShareName { get; set; }

    [Parameter(Mandatory = false)]
    public string? StorageAccountKey { get; set; }

    [Parameter(Mandatory = false)]
    public string? SasToken { get; set; }

    [Parameter(ParameterSetName = GetParameterSet, Mandatory = false)]
    public string? Snapshot { get; set; }

    [Parameter(ParameterSetName = GetParameterSet, Mandatory = false)]
    public string? LeaseId { get; set; }

    [Parameter(Mandatory = false)]
    public int Timeout { get; set; }

    [Parameter(Mandatory = false)]
    public string? ClientRequestId { get; set; }

    [Parameter(ParameterSetName = ListParameterSet, Mandatory = false)]
    public string? Prefix { get; set; }

    [Parameter(ParameterSetName = ListParameterSet, Mandatory = false)]
    public string? Marker { get; set; }

    [Parameter(ParameterSetName = ListParameterSet, Mandatory = false)]
    public int Maxresults { get; set; }

    [Parameter(ParameterSetName = ListParameterSet, Mandatory = false)]
    [ValidateSet("snapshots", "metadata", "deleted", IgnoreCase = true)]
    public string[]? Include { get; set; }

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
        if (MyInvocation.BoundParameters.ContainsKey(nameof(StorageAccountKey))) { parameters.Add("StorageAccountKey", StorageAccountKey!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(SasToken))) { parameters.Add("SasToken", SasToken!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Snapshot))) { parameters.Add("Snapshot", Snapshot!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(LeaseId))) { parameters.Add("LeaseId", LeaseId!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Timeout))) { parameters.Add("Timeout", Timeout); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ClientRequestId))) { parameters.Add("ClientRequestId", ClientRequestId!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Prefix))) { parameters.Add("Prefix", Prefix!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Marker))) { parameters.Add("Marker", Marker!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Maxresults))) { parameters.Add("Maxresults", Maxresults); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Include))) { parameters.Add("Include", Include!); }

        var moduleName = MyInvocation.MyCommand.ModuleName;
        var moduleVersion = MyInvocation.MyCommand.Version?.ToString();
        var invocation = new CommandInvocation("Storage", $"Get-AzStorageShare_{ParameterSetName}", parameters, false, moduleName, moduleVersion);
        telemetryService.TagInvocation(invocation);

        using ISession session = engine.OpenExecutionSession(invocation, streams, _cts.Token);
        session.Run();
    }

    protected override void EndProcessing() => _cts.Dispose();

    protected override void StopProcessing() => _cts.Cancel();
}
