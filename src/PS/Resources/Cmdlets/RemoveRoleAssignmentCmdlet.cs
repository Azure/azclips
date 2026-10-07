// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Resources;

/// <summary>
/// Deletes an Azure RBAC role assignment by scope and name (ARM RoleAssignments - Delete). The scope
/// defaults to the active subscription and can be narrowed with -ResourceGroupName or overridden with
/// -Scope (mutually exclusive). -Name (the assignment's GUID) is required: this targets exactly one
/// already-known assignment. Deleting by principal/role filter, or deleting every assignment at a
/// scope, is not supported — use Get-AzRoleAssignment first to find the assignment's name.
/// </summary>
[Cmdlet(VerbsCommon.Remove, "AzRoleAssignment", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High, DefaultParameterSetName = "SubscriptionScope")]
[OutputType("Microsoft.Azure.Azclips.Extensions.Resources.Models.RoleAssignment")]
public class RemoveRoleAssignmentCmdlet : PSCmdlet
{
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(ParameterSetName = "SubscriptionScope", Mandatory = false)]
    [Parameter(ParameterSetName = "ResourceGroup", Mandatory = false)]
    public string SubscriptionId { get; set; } = "";

    [Parameter(ParameterSetName = "ResourceGroup", Mandatory = true)]
    public string? ResourceGroupName { get; set; }

    [Parameter(ParameterSetName = "Scope", Mandatory = true)]
    public string? Scope { get; set; }

    [Parameter(Mandatory = true)]
    [ValidateRoleAssignmentName]
    public string Name { get; set; } = "";

    [Parameter(Mandatory = false)]
    public string? Profile { get; set; }

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "RoleAssignmentError", ErrorCategory.NotSpecified);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        string target = ResolveShouldProcessTarget();
        if (!ShouldProcess($"role assignment '{Name}' at {target}", "Remove"))
        {
            return;
        }

        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetryService.StartRootActivity();

        var parameters = new Dictionary<string, object>();
        if (MyInvocation.BoundParameters.ContainsKey(nameof(SubscriptionId)))    { parameters.Add("SubscriptionId", SubscriptionId); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ResourceGroupName))) { parameters.Add("ResourceGroupName", ResourceGroupName!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Scope)))             { parameters.Add("Scope", Scope!); }
        parameters.Add("Name", Name);
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Profile)))          { parameters.Add("Profile", Profile!); }

        var moduleName = MyInvocation.MyCommand.ModuleName;
        var moduleVersion = MyInvocation.MyCommand.Version?.ToString();
        var invocation = new CommandInvocation("Resources", "Remove-AzRoleAssignment", parameters, false, moduleName, moduleVersion);
        telemetryService.TagInvocation(invocation);

        using ISession session = engine.OpenExecutionSession(invocation, streams, _cts.Token);
        session.Run();
    }

    protected override void EndProcessing()
    {
        _cts.Dispose();
    }

    protected override void StopProcessing() => _cts.Cancel();

    private string ResolveShouldProcessTarget()
    {
        if (Scope is not null)
        {
            return Scope;
        }

        string subscription = DescribeSubscription();
        return ResourceGroupName is not null
            ? $"resource group '{ResourceGroupName}' in {subscription}"
            : subscription;
    }

    private string DescribeSubscription()
    {
        if (MyInvocation.BoundParameters.ContainsKey(nameof(SubscriptionId)))
        {
            return $"subscription '{SubscriptionId}'";
        }

        if (MyInvocation.BoundParameters.ContainsKey(nameof(Profile)))
        {
            return $"the subscription of profile '{Profile}'";
        }

        return "the active subscription";
    }
}
