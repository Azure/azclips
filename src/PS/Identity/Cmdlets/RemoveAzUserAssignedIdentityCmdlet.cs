// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Identity;

[Cmdlet(VerbsCommon.Remove, "AzUserAssignedIdentity", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
[OutputType(typeof(void))]
public class RemoveAzUserAssignedIdentityCmdlet : PSCmdlet
{
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(Mandatory = false)]
    public string SubscriptionId { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string ResourceGroupName { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string Name { get; set; } = "";

    [Parameter(Mandatory = false)]
    public string? Profile { get; set; }

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "UserAssignedIdentityError", ErrorCategory.NotSpecified);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        if (!ShouldProcess($"user assigned identity '{Name}' in resource group '{ResourceGroupName}'", "Remove"))
        {
            return;
        }

        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetryService.StartRootActivity();
        var parameters = new Dictionary<string, object>
        {
            { nameof(ResourceGroupName), ResourceGroupName },
            { nameof(Name), Name },
        };

        if (MyInvocation.BoundParameters.ContainsKey(nameof(SubscriptionId))) { parameters.Add(nameof(SubscriptionId), SubscriptionId); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Profile))) { parameters.Add(nameof(Profile), Profile!); }

        var moduleName = MyInvocation.MyCommand.ModuleName;
        var moduleVersion = MyInvocation.MyCommand.Version?.ToString();
        var invocation = new CommandInvocation("Identity", "Remove-AzUserAssignedIdentity", parameters, false, moduleName, moduleVersion);
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

