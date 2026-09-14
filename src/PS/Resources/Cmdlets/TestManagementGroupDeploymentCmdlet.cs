// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Resources;

[Cmdlet(VerbsDiagnostic.Test, "AzManagementGroupDeployment")]
[OutputType("Microsoft.Azure.Azclips.Extensions.Resources.Models.DeploymentValidateResult")]
public class TestManagementGroupDeploymentCmdlet : PSCmdlet
{
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams _streams = null!;
    private IEngine _engine = null!;

    [Parameter(Mandatory = true)]
    public string ManagementGroupId { get; set; } = "";

    [Alias("DeploymentName")]
    [Parameter]
    public string? Name { get; set; }

    [Parameter(Mandatory = true)]
    public string Location { get; set; } = "";

    [Parameter]
    public string? TemplateFile { get; set; }

    [Parameter]
    public string? TemplateUri { get; set; }

    [Parameter]
    public string? TemplateSpecId { get; set; }

    [Parameter]
    public Hashtable? TemplateObject { get; set; }

    [Parameter]
    public string? QueryString { get; set; }

    [Parameter]
    public string? TemplateParameterFile { get; set; }

    [Parameter]
    public string? TemplateParameterUri { get; set; }

    [Parameter]
    public Hashtable? TemplateParameterObject { get; set; }

    [Parameter]
    public string? ValidationLevel { get; set; }

    [Parameter]
    public string? Profile { get; set; }

    protected override void BeginProcessing()
    {
        _streams = CmdletOutputStreamsFactory.Create(this, "DeploymentError", ErrorCategory.NotSpecified);
        _engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        Dictionary<string, object> parameters = new()
        {
            [nameof(ManagementGroupId)] = ManagementGroupId,
            [nameof(Location)] = Location,
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
        AddIfBound(parameters, nameof(Profile), Profile);

        ITelemetryService telemetry = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetry.StartRootActivity();
        CommandInvocation invocation = new(
            "Resources",
            "Test-AzManagementGroupDeployment",
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
