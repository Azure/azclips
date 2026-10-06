// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Resources;

[Cmdlet(VerbsCommon.Get, "AzDeployment", DefaultParameterSetName = "List")]
[Alias("Get-AzSubscriptionDeployment")]
[OutputType("Microsoft.Azure.Azclips.Extensions.Resources.Models.DeploymentExtended")]
public class GetSubscriptionDeploymentCmdlet : PSCmdlet
{
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(ParameterSetName = "Get", Mandatory = false)]
    [Parameter(ParameterSetName = "List", Mandatory = false)]
    [Parameter(ParameterSetName = "GetById", Mandatory = false)]
    public string SubscriptionId { get; set; } = "";

    [Alias("DeploymentName")]
    [Parameter(ParameterSetName = "Get", Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true)]
    public string? Name { get; set; }

    [Parameter(ParameterSetName = "List", Mandatory = false)]
    public string? Filter { get; set; }

    [Alias("DeploymentId", "ResourceId")]
    [Parameter(ParameterSetName = "GetById", Mandatory = true)]
    public string? Id { get; set; }

    [Parameter(Mandatory = false)]
    public string? Profile { get; set; }

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "DeploymentError", ErrorCategory.NotSpecified, enumerateOutput: true);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetryService.StartRootActivity();
        var parameters = new Dictionary<string, object>();
        if (MyInvocation.BoundParameters.ContainsKey(nameof(SubscriptionId)))
        {
            parameters.Add("SubscriptionId", SubscriptionId);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Name)))
        {
            parameters.Add("Name", Name!);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Filter)))
        {
            parameters.Add("Filter", Filter!);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Id)))
        {
            parameters.Add("Id", Id!);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Profile)))
        {
            parameters.Add("Profile", Profile!);
        }

        var commandName = $"Get-AzDeployment_{ParameterSetName}";
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
