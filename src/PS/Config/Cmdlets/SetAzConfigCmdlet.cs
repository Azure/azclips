// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;

namespace Microsoft.Azure.Azclips.Cmdlets.Config;

[Cmdlet(VerbsCommon.Set, "AzConfig", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
[OutputType(typeof(string))]
public class SetAzConfigCmdlet : ConfigCmdletBase
{
    [Parameter(Mandatory = true)]
    public string Key { get; set; } = string.Empty;

    [Parameter(Mandatory = true)]
    public string Value { get; set; } = string.Empty;

    [Parameter(Mandatory = false)]
    public SwitchParameter Force { get; set; }

    protected override void ProcessRecord()
    {

        if (!ShouldProcess($"config key '{Key}'", "set"))
        {
            return;
        }

        var parameters = new Dictionary<string, object>();
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Key)))
        {
            parameters.Add("Key", Key);
        }

        if (MyInvocation.BoundParameters.ContainsKey(nameof(Value)))
        {
            parameters.Add("Value", Value);
        }

        if (MyInvocation.BoundParameters.ContainsKey(nameof(Force)))
        {
            parameters.Add("Force", Force.IsPresent);
        }

        ExecuteInvocation(parameters);
    }
}
