// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Resources;

[Cmdlet(VerbsCommon.Get, "AzResourceGroupDeployment", DefaultParameterSetName = "List")]
[OutputType("Microsoft.Azure.Azclips.Extensions.Resources.Models.DeploymentExtended")]
public class GetResourceGroupDeploymentCmdlet : PSCmdlet
{
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(ParameterSetName = "Get", Mandatory = false)]
    [Parameter(ParameterSetName = "List", Mandatory = false)]
    public string SubscriptionId { get; set; } = "";

    [Parameter(ParameterSetName = "Get", Position = 0, Mandatory = true, ValueFromPipelineByPropertyName = true)]
    [Parameter(ParameterSetName = "List", Position = 0, Mandatory = true, ValueFromPipelineByPropertyName = true)]
    public string ResourceGroupName { get; set; } = "";

    [Parameter(ParameterSetName = "Get", Position = 1, Mandatory = true, ValueFromPipelineByPropertyName = true)]
    [Parameter(ParameterSetName = "List", Position = 1, Mandatory = false, ValueFromPipelineByPropertyName = true)]
    [Alias("DeploymentName")]
    public string? Name { get; set; }

    [Parameter(ParameterSetName = "GetById", Mandatory = true)]
    [Alias("DeploymentId", "ResourceId")]
    [ValidateNotNullOrEmpty]
    public string? Id { get; set; }

    [Parameter(Mandatory = false)]
    public string? Profile { get; set; }

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "ResourceGroupDeploymentError", ErrorCategory.NotSpecified, enumerateOutput: true);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetryService.StartRootActivity();
        var parameters = new Dictionary<string, object>();
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ResourceGroupName)))
        {
            parameters.Add("ResourceGroupName", ResourceGroupName);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Id)))
        {
            parameters.Add("Id", Id!);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(SubscriptionId)))
        {
            parameters.Add("SubscriptionId", SubscriptionId);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Name)))
        {
            parameters.Add("Name", Name!);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Profile)))
        {
            parameters.Add("Profile", Profile!);
        }

        // Name may arrive via pipeline after the default "List" set is already chosen.
        var setName = ParameterSetName == "List" && parameters.ContainsKey("Name") ? "Get" : ParameterSetName;
        var commandName = $"Get-AzResourceGroupDeployment_{setName}";

        var moduleName = MyInvocation.MyCommand.ModuleName;
        var moduleVersion = MyInvocation.MyCommand.Version?.ToString();
        var invocation = new CommandInvocation("Resources", commandName, parameters, false, moduleName, moduleVersion);
        telemetryService.TagInvocation(invocation);

        using ISession session = engine.OpenExecutionSession(invocation, streams, _cts.Token);
        session.Run();
    }

    protected override void EndProcessing()
    {
        _cts.Dispose();
    }

    protected override void StopProcessing() => _cts.Cancel();
}
