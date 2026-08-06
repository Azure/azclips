// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;

namespace Microsoft.Azure.Azclips.Cmdlets.Config;

[Cmdlet(VerbsCommon.Remove, "AzConfig", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
[OutputType(typeof(string))]
public class RemoveAzConfigCmdlet : ConfigCmdletBase
{
    [Parameter(Mandatory = true)]
    public string Key { get; set; } = string.Empty;

    [Parameter(Mandatory = false)]
    public SwitchParameter Force { get; set; }

    protected override void ProcessRecord()
    {

        if (!ShouldProcess($"config key '{Key}'", "delete"))
        {
            return;
        }

        var parameters = new Dictionary<string, object>();
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Key)))
        {
            parameters.Add("Key", Key);
        }

        if (MyInvocation.BoundParameters.ContainsKey(nameof(Force)))
        {
            parameters.Add("Force", Force.IsPresent);
        }

        ExecuteInvocation(parameters);
    }
}
