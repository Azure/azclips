// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Resources;

[Cmdlet(VerbsCommon.New, "AzTenantDeployment", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
[OutputType(
    "Microsoft.Azure.Azclips.Extensions.Resources.Models.DeploymentExtended",
    "Microsoft.Azure.Azclips.Extensions.Resources.Models.WhatIfOperationResult")]
public class NewTenantDeploymentCmdlet : PSCmdlet
{
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Alias("DeploymentName")]
    [Parameter(Mandatory = false)]
    public string? Name { get; set; }

    [Parameter(Mandatory = true)]
    public string Location { get; set; } = string.Empty;

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
    public string[]? Parameter { get; set; }

    [Parameter(Mandatory = false)]
    public string? ValidationLevel { get; set; }

    [Parameter(Mandatory = false)]
    public Hashtable? Tag { get; set; }

    [Parameter(Mandatory = false)]
    public string? DeploymentDebugLogLevel { get; set; }

    [Parameter(Mandatory = false)]
    public string? WhatIfResultFormat { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? WhatIfExcludeChangeType { get; set; }

    [Parameter(Mandatory = false)]
    public SwitchParameter ProceedIfNoChange { get; set; }

    [Parameter(Mandatory = false)]
    public string? Profile { get; set; }

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "TenantDeploymentError", ErrorCategory.NotSpecified);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        bool armWhatIf = MyInvocation.BoundParameters.TryGetValue("WhatIf", out object? whatIf)
            && whatIf is SwitchParameter whatIfSwitch
            && whatIfSwitch.IsPresent;
        string target = $"tenant deployment '{Name ?? "(generated name)"}'";
        if (!armWhatIf && !ShouldProcess(target, "Create"))
        {
            return;
        }

        Dictionary<string, object> parameters = new()
        {
            [nameof(Location)] = Location,
        };
        AddBound(parameters, nameof(Name), Name);
        AddBound(parameters, nameof(TemplateFile), TemplateFile);
        AddBound(parameters, nameof(TemplateUri), TemplateUri);
        AddBound(parameters, nameof(TemplateSpecId), TemplateSpecId);
        AddBound(parameters, nameof(TemplateObject), TemplateObject);
        AddBound(parameters, nameof(QueryString), QueryString);
        AddBound(parameters, nameof(TemplateParameterFile), TemplateParameterFile);
        AddBound(parameters, nameof(TemplateParameterUri), TemplateParameterUri);
        AddBound(parameters, nameof(TemplateParameterObject), TemplateParameterObject);
        AddBound(parameters, nameof(Parameter), Parameter);
        AddBound(parameters, nameof(ValidationLevel), ValidationLevel);
        AddBound(parameters, nameof(DeploymentDebugLogLevel), DeploymentDebugLogLevel);
        AddBound(parameters, nameof(WhatIfResultFormat), WhatIfResultFormat);
        AddBound(parameters, nameof(WhatIfExcludeChangeType), WhatIfExcludeChangeType);
        AddBound(parameters, nameof(Profile), Profile);
        if (Tag is not null)
        {
            parameters[nameof(Tag)] = Tag.Cast<DictionaryEntry>()
                .ToDictionary(entry => entry.Key.ToString()!, entry => entry.Value?.ToString() ?? string.Empty);
        }
        if (ProceedIfNoChange.IsPresent)
        {
            parameters[nameof(ProceedIfNoChange)] = true;
        }
        if (armWhatIf)
        {
            parameters["WhatIf"] = true;
        }

        Run("New-AzTenantDeployment", parameters);
    }

    private void AddBound(Dictionary<string, object> parameters, string name, object? value)
    {
        if (MyInvocation.BoundParameters.ContainsKey(name) && value is not null)
        {
            parameters[name] = value;
        }
    }

    private void Run(string command, Dictionary<string, object> parameters)
    {
        ITelemetryService telemetry = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetry.StartRootActivity();
        CommandInvocation invocation = new("Resources", command, parameters, false, MyInvocation.MyCommand.ModuleName, MyInvocation.MyCommand.Version?.ToString());
        telemetry.TagInvocation(invocation);
        using ISession session = engine.OpenExecutionSession(invocation, streams, _cts.Token);
        session.Run();
    }

    protected override void EndProcessing() => _cts.Dispose();

    protected override void StopProcessing() => _cts.Cancel();
}
