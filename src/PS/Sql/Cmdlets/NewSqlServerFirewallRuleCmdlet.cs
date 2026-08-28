// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Sql;

[Cmdlet(VerbsCommon.New, "AzSqlServerFirewallRule",
        DefaultParameterSetName = IpRangeParameterSet,
        SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
[OutputType("Microsoft.Azure.Azclips.Extensions.Sql.Models.SqlFirewallRule")]
public class NewSqlServerFirewallRuleCmdlet : PSCmdlet
{
    private const string IpRangeParameterSet = "UseIpAddressRange";
    private const string AllowAllAzureIPsParameterSet = "AllowAllAzureIPs";

    // Az.Sql's well-known rule for "allow Azure services"; 0.0.0.0-0.0.0.0 is ARM's
    // convention for all Azure-internal IPs, not a literal address.
    private const string AllowAllAzureIPsRuleName = "AllowAllWindowsAzureIps";
    private const string AllowAllAzureIPsAddress = "0.0.0.0";

    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(Mandatory = true)]
    public string SubscriptionId { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string ResourceGroupName { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string ServerName { get; set; } = "";

    [Parameter(ParameterSetName = IpRangeParameterSet, Mandatory = true)]
    public string FirewallRuleName { get; set; } = "";

    [Parameter(ParameterSetName = IpRangeParameterSet, Mandatory = true)]
    public string StartIpAddress { get; set; } = "";

    [Parameter(ParameterSetName = IpRangeParameterSet, Mandatory = true)]
    public string EndIpAddress { get; set; } = "";

    [Parameter(ParameterSetName = AllowAllAzureIPsParameterSet, Mandatory = true)]
    public SwitchParameter AllowAllAzureIPs { get; set; }

    [Parameter(Mandatory = false)]
    public string? Profile { get; set; }

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "SqlServerFirewallRuleError", ErrorCategory.NotSpecified);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetryService.StartRootActivity();
        var parameters = new Dictionary<string, object>
        {
            { "SubscriptionId", SubscriptionId },
            { "ResourceGroupName", ResourceGroupName },
            { "ServerName", ServerName },
        };

        // -AllowAllAzureIPs is sugar for the well-known 0.0.0.0-0.0.0.0 rule. Resolving it here
        // keeps the processor a dumb three-string PUT and avoids a second CommandMetadata entry.
        if (AllowAllAzureIPs.IsPresent)
        {
            parameters.Add("FirewallRuleName", AllowAllAzureIPsRuleName);
            parameters.Add("StartIpAddress", AllowAllAzureIPsAddress);
            parameters.Add("EndIpAddress", AllowAllAzureIPsAddress);
        }
        else
        {
            parameters.Add("FirewallRuleName", FirewallRuleName);
            parameters.Add("StartIpAddress", StartIpAddress);
            parameters.Add("EndIpAddress", EndIpAddress);
        }

        if (MyInvocation.BoundParameters.ContainsKey(nameof(Profile)))
        {
            parameters.Add("Profile", Profile!);
        }

        var moduleName = MyInvocation.MyCommand.ModuleName;
        var moduleVersion = MyInvocation.MyCommand.Version?.ToString();
        var invocation = new CommandInvocation("Sql", "New-AzSqlServerFirewallRule", parameters, false, moduleName, moduleVersion);
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
