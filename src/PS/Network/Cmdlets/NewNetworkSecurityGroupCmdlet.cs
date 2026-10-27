// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Network;

[Cmdlet(VerbsCommon.New, "AzNetworkSecurityGroup", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
[OutputType("Microsoft.Azure.Azclips.Extensions.Network.Models.NetworkSecurityGroup")]
public class NewNetworkSecurityGroupCmdlet : PSCmdlet
{
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(Mandatory = false)]
    public string SubscriptionId { get; set; } = "";

    [Parameter(Mandatory = true, ValueFromPipelineByPropertyName = true)]
    [Alias("ResourceName")]
    public string Name { get; set; } = "";

    [Parameter(Mandatory = true, ValueFromPipelineByPropertyName = true)]
    public string ResourceGroupName { get; set; } = "";

    [Parameter(Mandatory = true, ValueFromPipelineByPropertyName = true)]
    public string Location { get; set; } = "";

    [Parameter(Mandatory = false, ValueFromPipelineByPropertyName = true)]
    public Hashtable? Tag { get; set; }

    [Parameter(Mandatory = false)]
    public string? Profile { get; set; }

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "NetworkSecurityGroupError", ErrorCategory.NotSpecified);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        if (!ShouldProcess($"Network security group '{Name}' in resource group '{ResourceGroupName}'", "Create"))
        {
            return;
        }

        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetryService.StartRootActivity();

        var parameters = new Dictionary<string, object>
        {
            { "Name", Name },
            { "ResourceGroupName", ResourceGroupName },
            { "Location", Location },
        };

        var bound = MyInvocation.BoundParameters;
        if (bound.ContainsKey(nameof(SubscriptionId))) { parameters.Add("SubscriptionId", SubscriptionId); }
        if (bound.ContainsKey(nameof(Tag)))            { parameters.Add("Tag", ToKeyValuePairs(Tag!)); }
        if (bound.ContainsKey(nameof(Profile)))        { parameters.Add("Profile", Profile!); }

        var moduleName = MyInvocation.MyCommand.ModuleName;
        var moduleVersion = MyInvocation.MyCommand.Version?.ToString();
        var invocation = new CommandInvocation("Network", "New-AzNetworkSecurityGroup", parameters, false, moduleName, moduleVersion);
        telemetryService.TagInvocation(invocation);

        using ISession session = engine.OpenExecutionSession(invocation, streams, _cts.Token);
        session.Run();
    }

    private static string[] ToKeyValuePairs(Hashtable table) =>
        table.Cast<DictionaryEntry>().Select(entry => $"{entry.Key}={entry.Value}").ToArray();

    protected override void EndProcessing() => _cts.Dispose();

    protected override void StopProcessing() => _cts.Cancel();
}
