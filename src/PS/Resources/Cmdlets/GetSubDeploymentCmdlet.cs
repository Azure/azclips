// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Cmdlets.Resources;

[Cmdlet(VerbsCommon.Get, "AzSubDeployment", DefaultParameterSetName = "List")]
[Alias("Get-AzSubscriptionDeployment")]
[OutputType("Microsoft.Azure.Azclips.Extensions.Resources.Models.DeploymentExtended")]
public class GetSubDeploymentCmdlet : PSCmdlet
{
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(ParameterSetName = "Get", Mandatory = false)]
    [Parameter(ParameterSetName = "List", Mandatory = false)]
    public string SubscriptionId { get; set; } = "";

    // No pipeline binding: a piped deployment must route to -InputObject so its Id (with subscription) is used.
    [Parameter(ParameterSetName = "Get", Position = 0, Mandatory = true)]
    [Parameter(ParameterSetName = "List", Position = 0, Mandatory = false)]
    [Alias("DeploymentName")]
    public string? Name { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "GetByInputObject", ValueFromPipeline = true, HelpMessage = "The deployment object.")]
    public DeploymentExtended? InputObject { get; set; }

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
        if (InputObject is not null)
        {
            // The piped deployment's Id carries its own subscription; the profile's subscription may differ.
            if (DeploymentObjectResolver.ParseSubscriptionDeploymentId(InputObject.Id) is not { } deployment)
            {
                WriteError(DeploymentObjectResolver.InvalidSubscriptionDeploymentIdError(InputObject));
                return;
            }
            parameters.Add("SubscriptionId", deployment.SubscriptionId);
            parameters.Add("Name", deployment.Name);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Profile)))
        {
            parameters.Add("Profile", Profile!);
        }

        // -Name is also in the default "List" set, so choose Get by the presence of a name, not by set.
        var commandName = parameters.ContainsKey("Name") ? "Get-AzSubDeployment_Get" : "Get-AzSubDeployment_List";

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
