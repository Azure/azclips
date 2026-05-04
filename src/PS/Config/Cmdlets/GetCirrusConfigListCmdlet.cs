using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Core.Telemetry;

namespace Microsoft.Azure.Cirrus.Cmdlets.Config;

[Cmdlet(VerbsCommon.Get, "CirrusConfigList")]
public class GetCirrusConfigListCmdlet : ConfigCmdletBase
{
    [Parameter(Mandatory = false)]
    public SwitchParameter IncludeDefaults { get; set; }

    [Parameter(Mandatory = false)]
    public SwitchParameter AsJson { get; set; }

    protected override void ProcessRecord()
    {
        using var activity = CirrusServiceProvider.GetRequiredService<ITelemetryService>().StartPowerShellActivity(MyInvocation.MyCommand.Name, MyInvocation.BoundParameters);

        var parameters = new Dictionary<string, object>();
        if (MyInvocation.BoundParameters.ContainsKey(nameof(IncludeDefaults)))
        {
            parameters.Add("All", IncludeDefaults.IsPresent);
        }

        if (MyInvocation.BoundParameters.ContainsKey(nameof(AsJson)))
        {
            parameters.Add("Json", AsJson.IsPresent);
        }

        ExecuteInvocation(parameters);
    }
}
