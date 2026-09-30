// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Resources;

/// <summary>
/// Creates an Azure RBAC role assignment (ARM RoleAssignments - Create). Assigns the role given by
/// -RoleDefinitionId to the principal given by -ObjectId at a scope. The scope defaults to the active
/// subscription and can be narrowed with -ResourceGroupName or overridden with -Scope (mutually
/// exclusive). -ObjectId and -RoleDefinitionId take GUIDs; principal name/UPN/service-principal and
/// role-name resolution are not yet supported (they require a Microsoft Graph or role-definition lookup
/// this command does not perform).
/// </summary>
[Cmdlet(VerbsCommon.New, "AzRoleAssignment", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium, DefaultParameterSetName = "SubscriptionScope")]
[OutputType("Microsoft.Azure.Azclips.Extensions.Resources.Models.RoleAssignment")]
public class NewRoleAssignmentCmdlet : PSCmdlet
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
    public string ObjectId { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string RoleDefinitionId { get; set; } = "";

    [Parameter(Mandatory = false)]
    public string? ObjectType { get; set; }

    [Parameter(Mandatory = false)]
    public string? Name { get; set; }

    [Parameter(Mandatory = false)]
    public string? Description { get; set; }

    [Parameter(Mandatory = false)]
    public string? Condition { get; set; }

    [Parameter(Mandatory = false)]
    public string? ConditionVersion { get; set; }

    [Parameter(Mandatory = false)]
    public string? Profile { get; set; }

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "RoleAssignmentError", ErrorCategory.NotSpecified);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        string target = Scope ?? (ResourceGroupName is null ? "the active subscription" : $"resource group '{ResourceGroupName}'");
        if (!ShouldProcess($"role assignment for principal '{ObjectId}' at {target}", "Create"))
        {
            return;
        }

        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetryService.StartRootActivity();

        var parameters = new Dictionary<string, object>();
        if (MyInvocation.BoundParameters.ContainsKey(nameof(SubscriptionId)))
        {
            parameters.Add("SubscriptionId", SubscriptionId);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ResourceGroupName)))
        {
            parameters.Add("ResourceGroupName", ResourceGroupName!);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Scope)))
        {
            parameters.Add("Scope", Scope!);
        }
        parameters.Add("ObjectId", ObjectId);
        parameters.Add("RoleDefinitionId", RoleDefinitionId);
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ObjectType)))
        {
            parameters.Add("ObjectType", ObjectType!);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Name)))
        {
            parameters.Add("Name", Name!);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Description)))
        {
            parameters.Add("Description", Description!);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Condition)))
        {
            parameters.Add("Condition", Condition!);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ConditionVersion)))
        {
            parameters.Add("ConditionVersion", ConditionVersion!);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Profile)))
        {
            parameters.Add("Profile", Profile!);
        }

        var moduleName = MyInvocation.MyCommand.ModuleName;
        var moduleVersion = MyInvocation.MyCommand.Version?.ToString();
        var invocation = new CommandInvocation("Resources", "New-AzRoleAssignment", parameters, false, moduleName, moduleVersion);
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
