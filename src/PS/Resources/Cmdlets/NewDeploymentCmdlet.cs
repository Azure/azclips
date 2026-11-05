// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Resources;

[Cmdlet(VerbsCommon.New, "AzDeployment", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
[Alias("New-AzSubscriptionDeployment")]
[OutputType("Microsoft.Azure.Azclips.Extensions.Resources.Models.DeploymentExtended")]
public class NewDeploymentCmdlet : PSCmdlet
{
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(Mandatory = true)]
    [Alias("DeploymentName")]
    public string Name { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string Location { get; set; } = "";

    [Parameter(Mandatory = true)]
    [ValidateSet("Incremental", "Complete")]
    public string Mode { get; set; } = "";

    [Parameter(Mandatory = false)]
    public string? TemplateUri { get; set; }

    [Parameter(Mandatory = false)]
    public string? TemplateSpecId { get; set; }

    [Parameter(Mandatory = false)]
    public Hashtable? TemplateObject { get; set; }

    [Parameter(Mandatory = false)]
    public string? QueryString { get; set; }

    [Parameter(Mandatory = false)]
    [Alias("TemplateParameterUri")]
    public string? ParametersUri { get; set; }

    [Parameter(Mandatory = false)]
    public string? ValidationLevel { get; set; }

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
        if (!ShouldProcess($"deployment '{Name}' at location '{Location}'", "Create"))
        {
            return;
        }

        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetryService.StartRootActivity();
        var parameters = new Dictionary<string, object> { { "Name", Name }, { "Location", Location }, { "Mode", Mode } };
        if (MyInvocation.BoundParameters.ContainsKey(nameof(SubscriptionId)))
        {
            parameters.Add("SubscriptionId", SubscriptionId!);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(TemplateUri)))
        {
            parameters.Add("TemplateUri", TemplateUri!);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(TemplateSpecId)))
        {
            parameters.Add("TemplateSpecId", TemplateSpecId!);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(TemplateObject)))
        {
            parameters.Add("TemplateObject", TemplateObject!);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(QueryString)))
        {
            parameters.Add("QueryString", QueryString!);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ParametersUri)))
        {
            parameters.Add("ParametersUri", ParametersUri!);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ValidationLevel)))
        {
            parameters.Add("ValidationLevel", ValidationLevel!);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Profile)))
        {
            parameters.Add("Profile", Profile!);
        }

        var moduleName = MyInvocation.MyCommand.ModuleName;
        var moduleVersion = MyInvocation.MyCommand.Version?.ToString();
        var invocation = new CommandInvocation("Resources", "New-AzDeployment", parameters, false, moduleName, moduleVersion);
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
