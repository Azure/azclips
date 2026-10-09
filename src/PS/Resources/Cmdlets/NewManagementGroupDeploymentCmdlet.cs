// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;

namespace Microsoft.Azure.Azclips.Cmdlets.Resources;

[Cmdlet(VerbsCommon.New, "AzManagementGroupDeployment", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
[OutputType(
    typeof(PSDeployment),
    typeof(WhatIfOperationResult))]
public class NewManagementGroupDeploymentCmdlet : PSCmdlet, IDynamicParameters
{
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;
    private RuntimeDefinedParameterDictionary? _templateParameters;

    [Parameter(Mandatory = true)]
    public string ManagementGroupId { get; set; } = string.Empty;

    [Alias("DeploymentName")]
    [Parameter(Mandatory = false)]
    public string? Name { get; set; }

    [Parameter(Mandatory = true)]
    public string Location { get; set; } = string.Empty;

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
    [Parameter(Mandatory = false)] public Hashtable? Tag { get; set; }
    [Parameter(Mandatory = false)] public string? DeploymentDebugLogLevel { get; set; }
    [Parameter(Mandatory = false)]
    [ValidateSet("FullResourcePayloads", "ResourceIdOnly", IgnoreCase = true)]
    public string? WhatIfResultFormat { get; set; }
    [Parameter(Mandatory = false)] public string[]? WhatIfExcludeChangeType { get; set; }
    [Parameter(Mandatory = false)] public SwitchParameter ProceedIfNoChange { get; set; }
    [Parameter(Mandatory = false, HelpMessage = "Run cmdlet in the background")] public SwitchParameter AsJob { get; set; }
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
            "ManagementGroupDeploymentError",
            ErrorCategory.NotSpecified);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        bool armWhatIf = MyInvocation.BoundParameters.TryGetValue("WhatIf", out object? whatIf)
            ? LanguagePrimitives.IsTrue(whatIf)
            : LanguagePrimitives.IsTrue(SessionState.PSVariable.GetValue("WhatIfPreference"));
        Dictionary<string, object> parameters = new()
        {
            [nameof(ManagementGroupId)] = ManagementGroupId,
            [nameof(Location)] = Location,
        };
        AddBound(parameters, nameof(Name), Name);
        AddBound(parameters, nameof(TemplateFile), TemplateFile);
        AddBound(parameters, nameof(TemplateUri), TemplateUri);
        AddBound(parameters, nameof(TemplateSpecId), TemplateSpecId);
        AddBound(
            parameters,
            nameof(TemplateObject),
            ManagementGroupDeploymentCmdletHelpers.NormalizeJsonInput(TemplateObject));
        AddBound(parameters, nameof(QueryString), QueryString);
        AddBound(parameters, nameof(TemplateParameterFile), TemplateParameterFile);
        AddBound(parameters, nameof(TemplateParameterUri), TemplateParameterUri);
        AddBound(
            parameters,
            nameof(TemplateParameterObject),
            ManagementGroupDeploymentCmdletHelpers.NormalizeJsonInput(TemplateParameterObject));
        AddBound(parameters, nameof(Parameter), Parameter);
        ManagementGroupDeploymentCmdletHelpers.AddTemplateParameterValues(
            parameters, _templateParameters, Parameter, MyInvocation.BoundParameters.ContainsKey(nameof(TemplateParameterUri)));
        AddBound(parameters, nameof(ValidationLevel), ValidationLevel);
        AddBound(parameters, nameof(DeploymentDebugLogLevel), DeploymentDebugLogLevel);
        AddBound(parameters, nameof(WhatIfResultFormat), WhatIfResultFormat);
        AddBound(parameters, nameof(WhatIfExcludeChangeType), WhatIfExcludeChangeType);
        AddBound(parameters, nameof(Profile), Profile);
        if (Tag is not null)
        {
            parameters[nameof(Tag)] = Tag.Cast<DictionaryEntry>()
                .ToDictionary(
                    entry => entry.Key.ToString()!,
                    entry => entry.Value?.ToString() ?? string.Empty);
        }
        if (armWhatIf)
        {
            parameters["WhatIf"] = true;
            ManagementGroupDeploymentCmdletHelpers.Run(
                this,
                engine,
                streams,
                _cts.Token,
                "New-AzManagementGroupDeployment",
                parameters);
            return;
        }

        bool confirm = MyInvocation.BoundParameters.TryGetValue("Confirm", out object? confirmValue)
            ? LanguagePrimitives.IsTrue(confirmValue)
            : SessionState.PSVariable.GetValue("ConfirmPreference") is ConfirmImpact preference
                && preference <= ConfirmImpact.Medium;
        bool skipConfirmation = false;
        if (confirm)
        {
            parameters["WhatIf"] = true;
            object? preview = ManagementGroupDeploymentCmdletHelpers.RunForOutput(
                this,
                engine,
                streams,
                _cts.Token,
                "New-AzManagementGroupDeployment",
                parameters);
            WriteInformation(preview, ["PSHOST"]);
            parameters.Remove("WhatIf");
            skipConfirmation = ProceedIfNoChange.IsPresent
                && preview is WhatIfOperationResult result
                && !Microsoft.Azure.Azclips.Extensions.Resources.Processors
                    .NewManagementGroupDeploymentProcessor_Orchestrator.HasChanges(result);
        }

        string target =
            $"management group '{ManagementGroupId}' deployment '{Name ?? "(generated name)"}'";
        if (!skipConfirmation && !ShouldProcess(target, "Create"))
        {
            return;
        }

        if (AsJob)
        {
            ManagementGroupDeploymentCmdletHelpers.RunAsJob(this, engine, "New-AzManagementGroupDeployment", parameters);
            return;
        }

        ManagementGroupDeploymentCmdletHelpers.Run(
            this,
            engine,
            streams,
            _cts.Token,
            "New-AzManagementGroupDeployment",
            parameters);
    }

    private void AddBound(Dictionary<string, object> parameters, string name, object? value)
    {
        if (MyInvocation.BoundParameters.ContainsKey(name) && value is not null)
        {
            parameters[name] = value;
        }
    }

    protected override void EndProcessing() => _cts.Dispose();

    protected override void StopProcessing() => _cts.Cancel();
}
