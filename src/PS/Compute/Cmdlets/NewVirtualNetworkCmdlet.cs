// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Compute;

[Cmdlet(VerbsCommon.New, "VirtualNetwork", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
public class NewVirtualNetworkCmdlet : PSCmdlet
{
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(Mandatory = false)]
    public string? Profile { get; set; }

    [Parameter(Mandatory = true)]
    public string SubscriptionId { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string Name { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string Location { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string ResourceGroupName { get; set; } = "";

    [Parameter(Mandatory = false)]
    public string[]? SubnetNames { get; set; }

    [Parameter(Mandatory = false)]
    public string[][]? SubnetAddressPrefixes { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? AddressPrefixes { get; set; }

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "VirtualNetworkError", ErrorCategory.NotSpecified);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetryService.StartRootActivity();
        var parameters = new Dictionary<string, object>
        {
            { "SubscriptionId", SubscriptionId },
            { "Name", Name },
            { "Location", Location },
            { "ResourceGroupName", ResourceGroupName },
        };
        if (MyInvocation.BoundParameters.ContainsKey(nameof(SubnetNames)))
        {
            parameters.Add("SubnetNames", SubnetNames!);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(SubnetAddressPrefixes)))
        {
            parameters.Add("SubnetAddressPrefixes", SubnetAddressPrefixes!);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(AddressPrefixes)))
        {
            parameters.Add("AddressPrefixes", AddressPrefixes!);
        }

        if (MyInvocation.BoundParameters.ContainsKey(nameof(Profile)))
        {
            parameters.Add("Profile", Profile!);
        }

        var moduleName = MyInvocation.MyCommand.ModuleName;
        var moduleVersion = MyInvocation.MyCommand.Version?.ToString();
        var invocation = new CommandInvocation("Compute", "New-VirtualNetwork", parameters, false, moduleName, moduleVersion);
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
