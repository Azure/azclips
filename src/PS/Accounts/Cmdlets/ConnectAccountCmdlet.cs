// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Accounts;

[Cmdlet(VerbsCommunications.Connect, CommandNoun, SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
[OutputType(typeof(Profile))]
public class ConnectAccountCmdlet : PSCmdlet
{
    private const string CommandNoun = Constants.CommandPrefix + "Account";
    private const string CommandName = VerbsCommunications.Connect + "-" + CommandNoun;
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(Mandatory = false)]
    [Alias("Subscription")]
    public string? SubscriptionId { get; set; }

    [Parameter(Mandatory = false)]
    [Alias("Tenant", "Domain")]
    public string? TenantId { get; set; }

    [Parameter(Mandatory = false)]
    [Alias("DeviceCode", "DeviceAuth", "Device", "UseDeviceAuthentication")]
    public SwitchParameter UseDeviceCode { get; set; }

    [Parameter(Mandatory = false)]
    public string? OutProfile { get; set; }

    [Parameter(Mandatory = false)]
    public string? Profile { get; set; }

    [Parameter(Mandatory = false)]
    [Alias("MSI", "ManagedIdentity", "ManagedService")]
    public SwitchParameter Identity { get; set; }

    [Parameter(Mandatory = false)]
    [Alias("AccountId")]
    public string? ClientId { get; set; }

    [Parameter(Mandatory = false)]
    public string? ObjectId { get; set; }

    [Parameter(Mandatory = false)]
    public string? ResourceId { get; set; }

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "LoginError", ErrorCategory.AuthenticationError);
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
        if (MyInvocation.BoundParameters.ContainsKey(nameof(TenantId)))
        {
            parameters.Add("TenantId", TenantId!);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(UseDeviceCode)))
        {
            parameters.Add("UseDeviceCode", UseDeviceCode.IsPresent);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(OutProfile)))
        {
            parameters.Add("OutProfile", OutProfile!);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Profile)))
        {
            parameters.Add("Profile", Profile!);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Identity)))
        {
            parameters.Add("Identity", Identity.IsPresent);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ClientId)))
        {
            parameters.Add("ClientId", ClientId!);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ObjectId)))
        {
            parameters.Add("ObjectId", ObjectId!);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ResourceId)))
        {
            parameters.Add("ResourceId", ResourceId!);
        }

        var moduleName = MyInvocation.MyCommand.ModuleName;
        var moduleVersion = MyInvocation.MyCommand.Version?.ToString();
        var invocation = new CommandInvocation(Constants.ExtensionName, CommandName, parameters, false, moduleName, moduleVersion);
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
