// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Resources;

[Cmdlet(VerbsCommon.Get, "AzDeploymentWhatIfResult")]
[Alias("Get-AzSubscriptionDeploymentWhatIfResult")]
[OutputType("Microsoft.Azure.Azclips.Extensions.Resources.Models.WhatIfOperationResult")]
public class GetDeploymentWhatIfResultCmdlet : PSCmdlet
{
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(Mandatory = false)]
    [Alias("DeploymentName")]
    public string? Name { get; set; }

    [Parameter(Mandatory = true)]
    public string Location { get; set; } = "";

    [Parameter(Mandatory = false)]
    public string? TemplateUri { get; set; }

    [Parameter(Mandatory = false)]
    public string? TemplateFile { get; set; }

    [Parameter(Mandatory = false)]
    public string? TemplateSpecId { get; set; }

    [Parameter(Mandatory = false)]
    public Hashtable? TemplateObject { get; set; }

    [Parameter(Mandatory = false)]
    public Hashtable? TemplateParameterObject { get; set; }

    [Parameter(Mandatory = false)]
    public string? QueryString { get; set; }

    [Parameter(Mandatory = false)]
    [Alias("TemplateParameterUri")]
    public string? ParametersUri { get; set; }

    [Parameter(Mandatory = false)]
    [Alias("TemplateParameterFile")]
    public string? ParametersFile { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? Parameter { get; set; }

    [Parameter(Mandatory = false)]
    public string? ValidationLevel { get; set; }

    [Parameter(Mandatory = false)]
    public string? ResultFormat { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? ExcludeChangeType { get; set; }

    [Parameter(Mandatory = false)]
    public string? SubscriptionId { get; set; }

    [Parameter(Mandatory = false)]
    public string? Profile { get; set; }

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "ResourcesError", ErrorCategory.NotSpecified);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetryService.StartRootActivity();
        var parameters = new Dictionary<string, object> { { "Location", Location } };
        if (MyInvocation.BoundParameters.ContainsKey(nameof(SubscriptionId)))
        {
            parameters.Add("SubscriptionId", SubscriptionId!);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Name)))
        {
            parameters.Add("Name", Name!);
        }
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
        if (MyInvocation.BoundParameters.ContainsKey(nameof(TemplateObject)))
        {
            parameters.Add("TemplateObject", TemplateObject!);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(TemplateParameterObject)))
        {
            parameters.Add("TemplateParameterObject", TemplateParameterObject!);
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
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ExcludeChangeType)))
        {
            parameters.Add("ExcludeChangeType", ExcludeChangeType!);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Profile)))
        {
            parameters.Add("Profile", Profile!);
        }

        var moduleName = MyInvocation.MyCommand.ModuleName;
        var moduleVersion = MyInvocation.MyCommand.Version?.ToString();
        var invocation = new CommandInvocation("Resources", "Get-AzDeploymentWhatIfResult", parameters, false, moduleName, moduleVersion);
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
