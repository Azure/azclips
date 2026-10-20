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

/// <summary>Shared engine plumbing for the *-AzTenantDeployment cmdlets: forwards bound parameters to the Resources extension.</summary>
public abstract class TenantDeploymentCmdletBase : PSCmdlet
{
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(Mandatory = false)]
    public string? Profile { get; set; }

    protected virtual bool EnumerateOutput => false;

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "TenantDeploymentError", ErrorCategory.NotSpecified, enumerateOutput: EnumerateOutput);
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

/// <summary>New-/Test-AzTenantDeployment inputs, the same as New-/Test-AzResourceGroupDeployment plus Location.</summary>
public abstract class TenantDeploymentTemplateCmdletBase : TenantDeploymentCmdletBase
{
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

    [Parameter(Mandatory = false)]
    public string[]? Parameter { get; set; }

    [Parameter(Mandatory = false)]
    public string? ValidationLevel { get; set; }
}

[Cmdlet(VerbsCommon.New, "AzTenantDeployment", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
[OutputType(typeof(DeploymentExtended))]
public class NewTenantDeploymentCmdlet : TenantDeploymentTemplateCmdletBase
{
    [Parameter(Mandatory = true)]
    public string Name { get; set; } = "";

    [Parameter(Mandatory = false)]
    public string? ParametersUri { get; set; }

    [Parameter(Mandatory = false)]
    public string? ParametersFile { get; set; }

    protected override void ProcessRecord()
    {
        if (ShouldProcess($"deployment '{Name}' at tenant scope", "Create"))
        {
            Invoke("New-AzTenantDeployment", GetBoundParameters());
        }
    }
}

// Validate is read-only, so like Test-AzResourceGroupDeployment it does not support ShouldProcess.
[Cmdlet(VerbsDiagnostic.Test, "AzTenantDeployment")]
[OutputType(typeof(DeploymentValidateResult))]
public class TestTenantDeploymentCmdlet : TenantDeploymentTemplateCmdletBase
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

    protected override void ProcessRecord() => Invoke("Test-AzTenantDeployment", GetBoundParameters());
}
[Cmdlet(VerbsCommon.Get, "AzTenantDeployment", DefaultParameterSetName = "GetByDeploymentName")]
[OutputType(typeof(DeploymentExtended))]
public class GetTenantDeploymentCmdlet : TenantDeploymentCmdletBase
{
    [Parameter(Mandatory = false, Position = 0, ParameterSetName = "GetByDeploymentName", HelpMessage = "The name of deployment.")]
    [Alias("DeploymentName")]
    [ValidateNotNullOrEmpty]
    public string? Name { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "GetByDeploymentId", HelpMessage = "The fully qualified resource Id of the deployment. example: /providers/Microsoft.Resources/deployments/{deploymentName}")]
    [Alias("DeploymentId", "ResourceId")]
    [ValidateNotNullOrEmpty]
    public string? Id { get; set; }

    protected override bool EnumerateOutput => true;

    protected override void ProcessRecord()
    {
        Invoke(Id is not null ? "Get-AzTenantDeployment_GetById"
            : string.IsNullOrEmpty(Name) ? "Get-AzTenantDeployment_List"
            : "Get-AzTenantDeployment_Get", GetBoundParameters());
    }
}

[Cmdlet(VerbsLifecycle.Stop, "AzTenantDeployment", SupportsShouldProcess = true, DefaultParameterSetName = "StopByDeploymentName")]
[OutputType(typeof(bool))]
public class StopTenantDeploymentCmdlet : TenantDeploymentCmdletBase
{
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = "StopByDeploymentName", HelpMessage = "The name of the deployment.")]
    [Alias("DeploymentName")]
    [ValidateNotNullOrEmpty]
    public string? Name { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "StopByDeploymentId", HelpMessage = "The fully qualified resource Id of the deployment. example: /providers/Microsoft.Resources/deployments/{deploymentName}")]
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
            Invoke("Stop-AzTenantDeployment", parameters);
        }

        if (PassThru.IsPresent)
        {
            WriteObject(true);
        }
    }
}

[Cmdlet(VerbsCommon.Remove, "AzTenantDeployment", SupportsShouldProcess = true, DefaultParameterSetName = "RemoveByDeploymentName")]
[OutputType(typeof(bool))]
public class RemoveTenantDeploymentCmdlet : TenantDeploymentCmdletBase
{
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = "RemoveByDeploymentName", HelpMessage = "The name of the deployment.")]
    [Alias("DeploymentName")]
    [ValidateNotNullOrEmpty]
    public string? Name { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "RemoveByDeploymentId", HelpMessage = "The fully qualified resource Id of the deployment. example: /providers/Microsoft.Resources/deployments/{deploymentName}")]
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
            Invoke("Remove-AzTenantDeployment", parameters);
            if (PassThru.IsPresent)
            {
                WriteObject(true);
            }
        }
    }
}
