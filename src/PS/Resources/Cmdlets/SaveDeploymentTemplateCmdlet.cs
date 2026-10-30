// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Resources;

[Cmdlet(VerbsData.Save, "AzDeploymentTemplate", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
[Alias("Save-AzSubscriptionDeploymentTemplate")]
[OutputType("Microsoft.Azure.Azclips.Extensions.Resources.Processors.DeploymentTemplateSaveResult")]
public class SaveDeploymentTemplateCmdlet : PSCmdlet
{
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(Mandatory = false)]
    public string SubscriptionId { get; set; } = "";

    [Alias("DeploymentName")]
    [Parameter(Mandatory = true, ValueFromPipelineByPropertyName = true)]
    public string Name { get; set; } = "";

    [Parameter(Mandatory = false, ValueFromPipelineByPropertyName = true)]
    public string? Path { get; set; }

    [Parameter(Mandatory = false)]
    public SwitchParameter Force { get; set; }

    [Parameter(Mandatory = false)]
    public string? Profile { get; set; }

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "DeploymentError", ErrorCategory.NotSpecified);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        if (!ShouldProcess($"deployment '{Name}'", "Save template"))
        {
            return;
        }

        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetryService.StartRootActivity();

        // Resolve the output path here: a relative path is relative to the PowerShell session's
        // current file-system location, which can differ from the process working directory the
        // processor would otherwise see.
        var parameters = new Dictionary<string, object>
        {
            { "Name", Name },
            { "Path", ResolveOutputPath() },
        };

        if (MyInvocation.BoundParameters.ContainsKey(nameof(SubscriptionId)))
        {
            parameters.Add("SubscriptionId", SubscriptionId);
        }

        if (Force.IsPresent)
        {
            parameters.Add("Force", true);
        }

        if (MyInvocation.BoundParameters.ContainsKey(nameof(Profile)))
        {
            parameters.Add("Profile", Profile!);
        }

        var moduleName = MyInvocation.MyCommand.ModuleName;
        var moduleVersion = MyInvocation.MyCommand.Version?.ToString();
        var invocation = new CommandInvocation("Resources", "Save-AzDeploymentTemplate", parameters, false, moduleName, moduleVersion);
        telemetryService.TagInvocation(invocation);

        using ISession session = engine.OpenExecutionSession(invocation, streams, _cts.Token);
        session.Run();
    }

    private string ResolveOutputPath() =>
        MyInvocation.BoundParameters.ContainsKey(nameof(Path))
            ? GetUnresolvedProviderPathFromPSPath(Path)
            : SessionState.Path.CurrentFileSystemLocation.Path;

    protected override void EndProcessing()
    {
        _cts.Dispose();
    }

    protected override void StopProcessing() => _cts.Cancel();
}
