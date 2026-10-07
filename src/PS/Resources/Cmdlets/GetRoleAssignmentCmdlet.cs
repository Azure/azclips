// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Resources;

/// <summary>
/// Lists Azure RBAC role assignments (ARM RoleAssignments - List For Scope). The scope defaults to
/// the active subscription and can be narrowed with -ResourceGroupName or overridden with -Scope
/// (mutually exclusive). -ObjectId filters server-side by principal object ID; -RoleDefinitionId
/// filters client-side by role definition ID. Name/UPN/service-principal resolution (-SignInName /
/// -ServicePrincipalName) and role-name resolution are not yet supported; they require a Microsoft
/// Graph or role-definition lookup this command does not perform.
/// </summary>
[Cmdlet(VerbsCommon.Get, "AzRoleAssignment", DefaultParameterSetName = "SubscriptionScope")]
[OutputType("Microsoft.Azure.Azclips.Extensions.Resources.Models.RoleAssignment")]
public class GetRoleAssignmentCmdlet : PSCmdlet
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

    [Parameter(ParameterSetName = "SubscriptionScope", Mandatory = false)]
    [Parameter(ParameterSetName = "ResourceGroup", Mandatory = false)]
    [Parameter(ParameterSetName = "Scope", Mandatory = false)]
    public string? ObjectId { get; set; }

    [Parameter(ParameterSetName = "SubscriptionScope", Mandatory = false)]
    [Parameter(ParameterSetName = "ResourceGroup", Mandatory = false)]
    [Parameter(ParameterSetName = "Scope", Mandatory = false)]
    public string? RoleDefinitionId { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "Include role assignments inherited from ancestor scopes. By default only assignments at the exact scope are listed.")]
    public SwitchParameter AtScope { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "When filtering by -ObjectId, also include assignments granted through groups the principal belongs to (transitive).")]
    public SwitchParameter ExpandPrincipalGroups { get; set; }

    [Parameter(Mandatory = false)]
    public string? Profile { get; set; }

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "RoleAssignmentError", ErrorCategory.NotSpecified, enumerateOutput: true);
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
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ResourceGroupName)))
        {
            parameters.Add("ResourceGroupName", ResourceGroupName!);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Scope)))
        {
            parameters.Add("Scope", Scope!);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ObjectId)))
        {
            parameters.Add("ObjectId", ObjectId!);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(RoleDefinitionId)))
        {
            parameters.Add("RoleDefinitionId", RoleDefinitionId!);
        }
        if (AtScope.IsPresent)
        {
            parameters.Add("AtScope", true);
        }
        if (ExpandPrincipalGroups.IsPresent)
        {
            parameters.Add("ExpandPrincipalGroups", true);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Profile)))
        {
            parameters.Add("Profile", Profile!);
        }

        var moduleName = MyInvocation.MyCommand.ModuleName;
        var moduleVersion = MyInvocation.MyCommand.Version?.ToString();
        var invocation = new CommandInvocation("Resources", "Get-AzRoleAssignment", parameters, false, moduleName, moduleVersion);
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
