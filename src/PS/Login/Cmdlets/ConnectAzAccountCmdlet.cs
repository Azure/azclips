using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Cirrus.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Login;

[Cmdlet(VerbsCommunications.Connect, "AzAccount", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
public class ConnectAzAccountCmdlet : PSCmdlet
{
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(Mandatory = true)]
    public string SubscriptionId { get; set; } = "";

    [Parameter(Mandatory = false)]
    public string? TenantId { get; set; }

    [Parameter(Mandatory = false)]
    public SwitchParameter UseDeviceCode { get; set; }

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "LoginError", ErrorCategory.AuthenticationError);
        engine = CirrusServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        var telemetryService = CirrusServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetryService.StartRootActivity();
        var parameters = new Dictionary<string, object>();
        if (MyInvocation.BoundParameters.ContainsKey(nameof(SubscriptionId)))
        {
            parameters.Add("SubscriptionId", SubscriptionId);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(TenantId)))
        {
            parameters.Add("TenantId", TenantId!);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(UseDeviceCode)))
        {
            parameters.Add("UseDeviceCode", UseDeviceCode.IsPresent);
        }

        var moduleName = MyInvocation.MyCommand.ModuleName;
        var moduleVersion = MyInvocation.MyCommand.Version?.ToString();
        var invocation = new CommandInvocation("Login", "Connect-AzAccount", parameters, false, moduleName, moduleVersion);
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
