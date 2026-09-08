// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Deployment;

[Cmdlet(VerbsCommon.New, "AzManagementGroupDeployment", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
[OutputType("Microsoft.Azure.Azclips.Extensions.Deployment.Models.DeploymentExtended")]
public class NewManagementGroupDeploymentCmdlet : PSCmdlet
{
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams _streams = null!;
    private IEngine _engine = null!;

    [Parameter(Mandatory = true)]
    public string ManagementGroupId { get; set; } = "";

    [Alias("DeploymentName")]
    [Parameter(Mandatory = false)]
    public string? Name { get; set; }

    [Parameter(Mandatory = true)]
    public string Location { get; set; } = "";

    [Parameter(Mandatory = false)]
    public string? TemplateFile { get; set; }

    [Parameter(Mandatory = false)]
    public string? TemplateUri { get; set; }

    [Parameter(Mandatory = false)]
    public string? TemplateSpecId { get; set; }

    [Parameter(Mandatory = false)]
    public Hashtable? TemplateObject { get; set; }

    [Parameter(Mandatory = false)]
    public string? QueryString { get; set; }

    [Parameter(Mandatory = false)]
    public string? TemplateParameterFile { get; set; }

    [Parameter(Mandatory = false)]
    public string? TemplateParameterUri { get; set; }

    [Parameter(Mandatory = false)]
    public Hashtable? TemplateParameterObject { get; set; }

    [Parameter(Mandatory = false)]
    public string? ValidationLevel { get; set; }

    [Parameter(Mandatory = false)]
    public string? DeploymentDebugLogLevel { get; set; }

    [Parameter(Mandatory = false)]
    public Hashtable? Tag { get; set; }

    [Parameter(Mandatory = false)]
    public string? Profile { get; set; }

    protected override void BeginProcessing()
    {
        _streams = CmdletOutputStreamsFactory.Create(this, "DeploymentError", ErrorCategory.NotSpecified);
        _engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        if (!ShouldProcess($"deployment '{Name ?? "(generated)"}' at management group '{ManagementGroupId}'", "Create"))
        {
            return;
        }

        Dictionary<string, object> parameters = new()
        {
            ["ManagementGroupId"] = ManagementGroupId,
            ["Location"] = Location,
        };
        AddIfBound(parameters, nameof(Name), Name);
        AddIfBound(parameters, nameof(TemplateFile), TemplateFile);
        AddIfBound(parameters, nameof(TemplateUri), TemplateUri);
        AddIfBound(parameters, nameof(TemplateSpecId), TemplateSpecId);
        AddIfBound(parameters, nameof(TemplateObject), TemplateObject);
        AddIfBound(parameters, nameof(QueryString), QueryString);
        AddIfBound(parameters, nameof(TemplateParameterFile), TemplateParameterFile);
        AddIfBound(parameters, nameof(TemplateParameterUri), TemplateParameterUri);
        AddIfBound(parameters, nameof(TemplateParameterObject), TemplateParameterObject);
        AddIfBound(parameters, nameof(ValidationLevel), ValidationLevel);
        AddIfBound(parameters, nameof(DeploymentDebugLogLevel), DeploymentDebugLogLevel);
        AddIfBound(parameters, nameof(Tag), Tag);
        AddIfBound(parameters, nameof(Profile), Profile);

        ITelemetryService telemetry = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetry.StartRootActivity();
        CommandInvocation invocation = new(
            "Deployment",
            "New-AzManagementGroupDeployment",
            parameters,
            false,
            MyInvocation.MyCommand.ModuleName,
            MyInvocation.MyCommand.Version?.ToString());
        telemetry.TagInvocation(invocation);

        using ISession session = _engine.OpenExecutionSession(invocation, _streams, _cts.Token);
        session.Run();
    }

    protected override void StopProcessing() => _cts.Cancel();

    protected override void EndProcessing() => _cts.Dispose();

    private void AddIfBound(Dictionary<string, object> parameters, string name, object? value)
    {
        if (MyInvocation.BoundParameters.ContainsKey(name))
        {
            parameters[name] = value!;
        }
    }
}
