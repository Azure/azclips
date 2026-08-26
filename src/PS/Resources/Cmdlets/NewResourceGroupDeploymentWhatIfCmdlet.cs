// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Resources;

// What-if is a read-only preview: it never creates or modifies resources, so it intentionally
// does NOT declare SupportsShouldProcess/ConfirmImpact (there is nothing to confirm).
[Cmdlet(VerbsCommon.New, "AzResourceGroupDeploymentWhatIf")]
[OutputType("Microsoft.Azure.Azclips.Extensions.Resources.Models.WhatIfOperationResult")]
public class NewResourceGroupDeploymentWhatIfCmdlet : PSCmdlet
{
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(Mandatory = true)]
    public string ResourceGroupName { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string Name { get; set; } = "";

    [Parameter(Mandatory = false)]
    public string? TemplateUri { get; set; }

    [Parameter(Mandatory = false)]
    public string? TemplateFile { get; set; }

    [Parameter(Mandatory = false)]
    public string? TemplateSpecId { get; set; }

    [Parameter(Mandatory = false)]
    public string? QueryString { get; set; }

    [Parameter(Mandatory = false)]
    public string? ParametersUri { get; set; }

    [Parameter(Mandatory = false)]
    public string? ParametersFile { get; set; }

    [Parameter(Mandatory = false)]
    public string? Mode { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? Parameter { get; set; }

    [Parameter(Mandatory = false)]
    public string? ValidationLevel { get; set; }

    [Parameter(Mandatory = false)]
    public string? ResultFormat { get; set; }

    [Parameter(Mandatory = false)]
    public string? SubscriptionId { get; set; }

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "ResourcesError", ErrorCategory.NotSpecified);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetryService.StartRootActivity();
        var parameters = new Dictionary<string, object>();
        if (MyInvocation.BoundParameters.ContainsKey(nameof(SubscriptionId)))
        {
            parameters.Add("SubscriptionId", SubscriptionId!);
        }
        parameters.Add("ResourceGroupName", ResourceGroupName);
        parameters.Add("Name", Name);
        if (MyInvocation.BoundParameters.ContainsKey(nameof(TemplateUri)))
        {
            parameters.Add("TemplateUri", TemplateUri!);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(TemplateFile)))
        {
            parameters.Add("TemplateFile", TemplateFile!);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(TemplateSpecId)))
        {
            parameters.Add("TemplateSpecId", TemplateSpecId!);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(QueryString)))
        {
            parameters.Add("QueryString", QueryString!);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ParametersUri)))
        {
            parameters.Add("ParametersUri", ParametersUri!);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ParametersFile)))
        {
            parameters.Add("ParametersFile", ParametersFile!);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Mode)))
        {
            parameters.Add("Mode", Mode!);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Parameter)))
        {
            parameters.Add("Parameter", Parameter!);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ValidationLevel)))
        {
            parameters.Add("ValidationLevel", ValidationLevel!);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ResultFormat)))
        {
            parameters.Add("ResultFormat", ResultFormat!);
        }

        var moduleName = MyInvocation.MyCommand.ModuleName;
        var moduleVersion = MyInvocation.MyCommand.Version?.ToString();
        var invocation = new CommandInvocation("Resources", "New-AzResourceGroupDeploymentWhatIf", parameters, false, moduleName, moduleVersion);
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
