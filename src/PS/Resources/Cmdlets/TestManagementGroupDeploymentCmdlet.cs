// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Resources;

[Cmdlet(VerbsDiagnostic.Test, "AzManagementGroupDeployment")]
public class TestManagementGroupDeploymentCmdlet : PSCmdlet, IDynamicParameters
{
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;
    private RuntimeDefinedParameterDictionary? _templateParameters;

    [Parameter(Mandatory = true)] public string ManagementGroupId { get; set; } = string.Empty;
    [Alias("DeploymentName")]
    [Parameter(Mandatory = false)] public string? Name { get; set; }
    [Parameter(Mandatory = true)] public string Location { get; set; } = string.Empty;
    [Parameter(Mandatory = false)] public string? TemplateFile { get; set; }
    [Parameter(Mandatory = false)] public string? TemplateUri { get; set; }
    [Parameter(Mandatory = false)] public string? TemplateSpecId { get; set; }
    [Parameter(Mandatory = false)] public Hashtable? TemplateObject { get; set; }
    [Parameter(Mandatory = false)] public string? QueryString { get; set; }
    [Parameter(Mandatory = false)] public string? TemplateParameterFile { get; set; }
    [Parameter(Mandatory = false)] public string? TemplateParameterUri { get; set; }
    [Parameter(Mandatory = false)] public Hashtable? TemplateParameterObject { get; set; }
    [Parameter(Mandatory = false)] public string[]? Parameter { get; set; }
    [Parameter(Mandatory = false)] public string? ValidationLevel { get; set; }
    [Parameter(Mandatory = false)] public SwitchParameter SuppressDiagnostics { get; set; }
    [Parameter(Mandatory = false)] public SwitchParameter SkipTemplateParameterPrompt { get; set; }
    // Legacy ResourceManagerCmdletBase switch; it only affects API version lookup, which deployments do not use.
    [Parameter(Mandatory = false)] public SwitchParameter Pre { get; set; }
    [Parameter(Mandatory = false)] public string? Profile { get; set; }

    public object GetDynamicParameters() =>
        _templateParameters = SkipTemplateParameterPrompt
            ? new RuntimeDefinedParameterDictionary()
            : ManagementGroupDeploymentCmdletHelpers.CreateTemplateParameters(
                this, TemplateObject, TemplateFile, TemplateUri, TemplateSpecId, QueryString,
                TemplateParameterObject, TemplateParameterFile, TemplateParameterUri);

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(
            this,
            "ManagementGroupDeploymentValidationError",
            ErrorCategory.InvalidData);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        Dictionary<string, object> parameters = new()
        {
            [nameof(ManagementGroupId)] = ManagementGroupId,
            [nameof(Location)] = Location,
        };
        foreach (string name in new[]
        {
            nameof(Name), nameof(TemplateFile), nameof(TemplateUri), nameof(TemplateSpecId),
            nameof(QueryString), nameof(TemplateParameterFile),
            nameof(TemplateParameterUri), nameof(Parameter),
            nameof(ValidationLevel), nameof(Profile),
        })
        {
            if (MyInvocation.BoundParameters.TryGetValue(name, out object? value) && value is not null)
            {
                parameters[name] = value;
            }
        }
        if (SuppressDiagnostics)
        {
            parameters[nameof(SuppressDiagnostics)] = true;
        }
        if (TemplateObject is not null)
        {
            parameters[nameof(TemplateObject)] =
                ManagementGroupDeploymentCmdletHelpers.NormalizeJsonInput(TemplateObject)!;
        }
        if (TemplateParameterObject is not null)
        {
            parameters[nameof(TemplateParameterObject)] =
                ManagementGroupDeploymentCmdletHelpers.NormalizeJsonInput(TemplateParameterObject)!;
        }
        ManagementGroupDeploymentCmdletHelpers.AddTemplateParameterValues(
            parameters, _templateParameters, Parameter, MyInvocation.BoundParameters.ContainsKey(nameof(TemplateParameterUri)));

        ManagementGroupDeploymentCmdletHelpers.Run(
            this,
            engine,
            streams,
            _cts.Token,
            "Test-AzManagementGroupDeployment",
            parameters);
    }

    protected override void EndProcessing() => _cts.Dispose();

    protected override void StopProcessing() => _cts.Cancel();
}
