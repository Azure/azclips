// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Management.Automation;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Cmdlets.Resources;

/// <summary>Shared engine plumbing for the *-AzManagementGroupDeployment cmdlets: forwards bound parameters to the Resources extension.</summary>
public abstract class ManagementGroupDeploymentCmdletBase : PSCmdlet
{
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(Mandatory = false)]
    public string? Profile { get; set; }

    protected virtual bool EnumerateOutput => false;

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "ManagementGroupDeploymentError", ErrorCategory.NotSpecified, enumerateOutput: EnumerateOutput);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected Dictionary<string, object> GetBoundParameters() =>
        MyInvocation.BoundParameters
            .Where(p => !CommonParameters.Contains(p.Key) && !OptionalCommonParameters.Contains(p.Key))
            .ToDictionary(p => p.Key, p => p.Value is SwitchParameter s ? s.IsPresent : p.Value);

    protected void Invoke(string commandName, Dictionary<string, object> parameters)
    {
        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetryService.StartRootActivity();
        var invocation = new CommandInvocation("Resources", commandName, parameters, false, MyInvocation.MyCommand.ModuleName, MyInvocation.MyCommand.Version?.ToString());
        telemetryService.TagInvocation(invocation);

        using ISession session = engine.OpenExecutionSession(invocation, streams, _cts.Token);
        session.Run();
    }

    protected override void EndProcessing() => _cts.Dispose();

    protected override void StopProcessing() => _cts.Cancel();
}

/// <summary>New-/Test-AzManagementGroupDeployment inputs, the same as New-/Test-AzResourceGroupDeployment plus ManagementGroupId and Location.</summary>
public abstract class ManagementGroupDeploymentTemplateCmdletBase : ManagementGroupDeploymentCmdletBase
{
    [Parameter(Mandatory = true)]
    public string ManagementGroupId { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string Location { get; set; } = "";

    [Parameter(Mandatory = false)]
    public string? TemplateUri { get; set; }

    [Parameter(Mandatory = false)]
    public string? TemplateFile { get; set; }

    [Parameter(Mandatory = false)]
    public string? TemplateSpecId { get; set; }

    [Parameter(Mandatory = false)]
    public string? QueryString { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "The create deployment mode. Incremental (default) or Complete. Validation always uses Incremental, regardless of this value.")]
    public string? Mode { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? Parameter { get; set; }

    [Parameter(Mandatory = false)]
    public string? ValidationLevel { get; set; }
}

[Cmdlet(VerbsCommon.New, "AzManagementGroupDeployment", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
[OutputType(typeof(DeploymentExtended))]
public class NewManagementGroupDeploymentCmdlet : ManagementGroupDeploymentTemplateCmdletBase
{
    [Parameter(Mandatory = true)]
    public string Name { get; set; } = "";

    [Parameter(Mandatory = false)]
    public string? ParametersUri { get; set; }

    [Parameter(Mandatory = false)]
    public string? ParametersFile { get; set; }

    protected override void ProcessRecord()
    {
        if (ShouldProcess($"deployment '{Name}' in management group '{ManagementGroupId}'", "Create"))
        {
            Invoke("New-AzManagementGroupDeployment", GetBoundParameters());
        }
    }
}

// Validate is read-only, so like Test-AzResourceGroupDeployment it does not support ShouldProcess.
[Cmdlet(VerbsDiagnostic.Test, "AzManagementGroupDeployment")]
[OutputType(typeof(DeploymentValidateResult))]
public class TestManagementGroupDeploymentCmdlet : ManagementGroupDeploymentTemplateCmdletBase
{
    [Parameter(Mandatory = false)]
    public string Name { get; set; } = "";

    [Parameter(Mandatory = false)]
    public Hashtable? TemplateObject { get; set; }

    [Parameter(Mandatory = false)]
    [Alias("TemplateParameterUri")]
    public string? ParametersUri { get; set; }

    [Parameter(Mandatory = false)]
    [Alias("TemplateParameterFile")]
    public string? ParametersFile { get; set; }

    protected override void ProcessRecord() => Invoke("Test-AzManagementGroupDeployment", GetBoundParameters());
}
[Cmdlet(VerbsCommon.Get, "AzManagementGroupDeployment", DefaultParameterSetName = "GetByDeploymentName")]
[OutputType(typeof(DeploymentExtended))]
public class GetManagementGroupDeploymentCmdlet : ManagementGroupDeploymentCmdletBase
{
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = "GetByDeploymentName", HelpMessage = "The management group id.")]
    [ValidateNotNullOrEmpty]
    public string? ManagementGroupId { get; set; }

    [Parameter(Mandatory = false, Position = 1, ParameterSetName = "GetByDeploymentName", HelpMessage = "The name of deployment.")]
    [Alias("DeploymentName")]
    [ValidateNotNullOrEmpty]
    public string? Name { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "GetByDeploymentId", HelpMessage = "The fully qualified resource Id of the deployment. example: /providers/Microsoft.Management/managementGroups/{managementGroupId}/providers/Microsoft.Resources/deployments/{deploymentName}")]
    [Alias("DeploymentId", "ResourceId")]
    [ValidateNotNullOrEmpty]
    public string? Id { get; set; }

    protected override bool EnumerateOutput => true;

    protected override void ProcessRecord()
    {
        Invoke(Id is not null ? "Get-AzManagementGroupDeployment_GetById"
            : string.IsNullOrEmpty(Name) ? "Get-AzManagementGroupDeployment_List"
            : "Get-AzManagementGroupDeployment_Get", GetBoundParameters());
    }
}

[Cmdlet(VerbsLifecycle.Stop, "AzManagementGroupDeployment", SupportsShouldProcess = true, DefaultParameterSetName = "StopByDeploymentName")]
[OutputType(typeof(bool))]
public class StopManagementGroupDeploymentCmdlet : ManagementGroupDeploymentCmdletBase
{
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = "StopByDeploymentName", HelpMessage = "The management group id.")]
    [ValidateNotNullOrEmpty]
    public string? ManagementGroupId { get; set; }

    [Parameter(Mandatory = true, Position = 1, ParameterSetName = "StopByDeploymentName", HelpMessage = "The name of the deployment.")]
    [Alias("DeploymentName")]
    [ValidateNotNullOrEmpty]
    public string? Name { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "StopByDeploymentId", HelpMessage = "The fully qualified resource Id of the deployment. example: /providers/Microsoft.Management/managementGroups/{managementGroupId}/providers/Microsoft.Resources/deployments/{deploymentName}")]
    [Alias("DeploymentId", "ResourceId")]
    [ValidateNotNullOrEmpty]
    public string? Id { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "StopByInputObject", ValueFromPipeline = true, HelpMessage = "The deployment object.")]
    public DeploymentExtended? InputObject { get; set; }

    [Parameter(Mandatory = false)]
    public SwitchParameter PassThru { get; set; }

    protected override void ProcessRecord()
    {
        var parameters = GetBoundParameters();
        parameters.Remove(nameof(InputObject));
        parameters.Remove(nameof(PassThru));
        if (InputObject?.Id is { } inputId) parameters[nameof(Id)] = inputId;

        // Legacy ConfirmAction targets -Name only, and -PassThru writes true even when declined.
        if (ShouldProcess(Name, "Cancelling active deployment ..."))
        {
            Invoke("Stop-AzManagementGroupDeployment", parameters);
        }

        if (PassThru.IsPresent)
        {
            WriteObject(true);
        }
    }
}

[Cmdlet(VerbsCommon.Remove, "AzManagementGroupDeployment", SupportsShouldProcess = true, DefaultParameterSetName = "RemoveByDeploymentName")]
[OutputType(typeof(bool))]
public class RemoveManagementGroupDeploymentCmdlet : ManagementGroupDeploymentCmdletBase
{
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = "RemoveByDeploymentName", HelpMessage = "The management group id.")]
    [ValidateNotNullOrEmpty]
    public string? ManagementGroupId { get; set; }

    [Parameter(Mandatory = true, Position = 1, ParameterSetName = "RemoveByDeploymentName", HelpMessage = "The name of the deployment.")]
    [Alias("DeploymentName")]
    [ValidateNotNullOrEmpty]
    public string? Name { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "RemoveByDeploymentId", HelpMessage = "The fully qualified resource Id of the deployment. example: /providers/Microsoft.Management/managementGroups/{managementGroupId}/providers/Microsoft.Resources/deployments/{deploymentName}")]
    [Alias("DeploymentId", "ResourceId")]
    [ValidateNotNullOrEmpty]
    public string? Id { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "RemoveByInputObject", ValueFromPipeline = true, HelpMessage = "The deployment object.")]
    public DeploymentExtended? InputObject { get; set; }

    [Parameter(Mandatory = false)]
    public SwitchParameter PassThru { get; set; }

    protected override void ProcessRecord()
    {
        var parameters = GetBoundParameters();
        parameters.Remove(nameof(InputObject));
        parameters.Remove(nameof(PassThru));
        if (InputObject?.Id is { } inputId) parameters[nameof(Id)] = inputId;

        // Legacy ConfirmAction targets -Name only; -PassThru writes true only when the delete ran.
        if (ShouldProcess(Name, "Deleting the deployment ..."))
        {
            Invoke("Remove-AzManagementGroupDeployment", parameters);
            if (PassThru.IsPresent)
            {
                WriteObject(true);
            }
        }
    }
}
