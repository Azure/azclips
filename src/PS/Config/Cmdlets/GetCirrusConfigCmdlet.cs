using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Core.Telemetry;

namespace Microsoft.Azure.Cirrus.Cmdlets.Config;

[Cmdlet(VerbsCommon.Get, "CirrusConfig")]
public class GetCirrusConfigCmdlet : ConfigCmdletBase
{
    [Parameter(Mandatory = true)]
    public string Key { get; set; } = string.Empty;

    [Parameter(Mandatory = false)]
    public SwitchParameter AsJson { get; set; }

    protected override void ProcessRecord()
    {
        using var activity = CirrusServiceProvider.GetRequiredService<ITelemetryService>().StartPowerShellActivity(MyInvocation.MyCommand.Name, MyInvocation.BoundParameters);

        var parameters = new Dictionary<string, object>();
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Key)))
        {
            parameters.Add("Key", Key);
        }

        if (MyInvocation.BoundParameters.ContainsKey(nameof(AsJson)))
        {
            parameters.Add("Json", AsJson.IsPresent);
        }

        ExecuteInvocation(parameters);
    }
}
