// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Storage;

[Cmdlet(VerbsCommon.Get, "AzStorageAccount", DefaultParameterSetName = ResourceGroupParameterSet)]
[OutputType("Microsoft.Azure.Azclips.Extensions.Storage.Management.Models.StorageAccount")]
public class GetStorageAccountCmdlet : PSCmdlet
{
    // The parameter set decides which API operation runs, so the names are part of the contract:
    // ProcessRecord appends the active one to the command name and the extension index binds each
    // to its own processor.
    private const string AccountNameParameterSet = "AccountName";
    private const string ResourceGroupParameterSet = "ResourceGroup";

    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(ParameterSetName = AccountNameParameterSet, Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true)]
    [Parameter(ParameterSetName = ResourceGroupParameterSet, Mandatory = false, Position = 0, ValueFromPipelineByPropertyName = true)]
    public string ResourceGroupName { get; set; } = "";

    [Parameter(ParameterSetName = AccountNameParameterSet, Mandatory = true, Position = 1, ValueFromPipelineByPropertyName = true)]
    [Alias("StorageAccountName", "AccountName")]
    public string? Name { get; set; }

    [Parameter(ParameterSetName = AccountNameParameterSet, Mandatory = false)]
    public SwitchParameter IncludeGeoReplicationStats { get; set; }

    [Parameter(ParameterSetName = AccountNameParameterSet, Mandatory = false)]
    [Parameter(ParameterSetName = ResourceGroupParameterSet, Mandatory = false)]
    public string SubscriptionId { get; set; } = "";

    [Parameter(Mandatory = false)]
    public string? Profile { get; set; }

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
        if (MyInvocation.BoundParameters.ContainsKey(nameof(SubscriptionId))) { parameters.Add("SubscriptionId", SubscriptionId); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ResourceGroupName))) { parameters.Add("ResourceGroupName", ResourceGroupName); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Name))) { parameters.Add("Name", Name!); }

        // The switch is the PowerShell spelling of the service's $expand query option. Azure
        // PowerShell exposes it the same way rather than as a free-text value.
        if (IncludeGeoReplicationStats.IsPresent) { parameters.Add("Expand", "geoReplicationStats"); }

        if (MyInvocation.BoundParameters.ContainsKey(nameof(Profile))) { parameters.Add("Profile", Profile!); }

        var commandName = $"Get-AzStorageAccount_{ParameterSetName}";
        var moduleName = MyInvocation.MyCommand.ModuleName;
        var moduleVersion = MyInvocation.MyCommand.Version?.ToString();
        var invocation = new CommandInvocation("Storage", commandName, parameters, false, moduleName, moduleVersion);
        telemetryService.TagInvocation(invocation);

        using ISession session = engine.OpenExecutionSession(invocation, streams, _cts.Token);
        session.Run();
    }

    protected override void EndProcessing() => _cts.Dispose();

    protected override void StopProcessing() => _cts.Cancel();
}
