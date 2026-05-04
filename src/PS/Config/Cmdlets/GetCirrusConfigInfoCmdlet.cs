using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Core.Telemetry;

namespace Microsoft.Azure.Cirrus.Cmdlets.Config;

[Cmdlet(VerbsCommon.Get, "CirrusConfigInfo")]
public class GetCirrusConfigInfoCmdlet : ConfigCmdletBase
{
    [Parameter(Mandatory = false)]
    public SwitchParameter KeysOnly { get; set; }

    protected override void ProcessRecord()
    {
        using var activity = CirrusServiceProvider.GetRequiredService<ITelemetryService>().StartPowerShellActivity(MyInvocation.MyCommand.Name, MyInvocation.BoundParameters);

        var parameters = new Dictionary<string, object>();
        if (MyInvocation.BoundParameters.ContainsKey(nameof(KeysOnly)))
        {
            parameters.Add("KeysOnly", KeysOnly.IsPresent);
        }

        ExecuteInvocation(parameters);
    }
}
