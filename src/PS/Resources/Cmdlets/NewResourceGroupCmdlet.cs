// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Resources;

[Cmdlet(VerbsCommon.New, "AzResourceGroup", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
[OutputType("Microsoft.Azure.Azclips.Extensions.Resources.Models.ResourceGroup")]
public class NewResourceGroupCmdlet : PSCmdlet
{
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(Mandatory = true)]
    public string Name { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string Location { get; set; } = "";

    [Parameter(Mandatory = false)]
    public string? SubscriptionId { get; set; }

    [Parameter(Mandatory = false)]
    public string? Profile { get; set; }

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "ResourceGroupError", ErrorCategory.NotSpecified);
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
        parameters.Add("Name", Name);
        parameters.Add("Location", Location);

        if (MyInvocation.BoundParameters.ContainsKey(nameof(Profile)))
        {
            parameters.Add("Profile", Profile!);
        }

        var moduleName = MyInvocation.MyCommand.ModuleName;
        var moduleVersion = MyInvocation.MyCommand.Version?.ToString();
        var invocation = new CommandInvocation("Resources", "New-AzResourceGroup", parameters, false, moduleName, moduleVersion);
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
