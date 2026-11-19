// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;

namespace Microsoft.Azure.Azclips.Cmdlets.Config;

[Cmdlet(VerbsCommon.Get, "AzConfig", DefaultParameterSetName = "List")]
[OutputType("Microsoft.Azure.Azclips.Extensions.Config.Models.ConfigEntry")]
public class GetAzConfigCmdlet : ConfigCmdletBase
{
    protected override bool EnumerateOutput => true;

    [Parameter(Mandatory = true, ParameterSetName = "Key")]
    public string Key { get; set; } = string.Empty;

    [Parameter(ParameterSetName = "List")]
    public SwitchParameter ConfiguredOnly { get; set; }

    protected override void ProcessRecord()
    {

        var parameters = new Dictionary<string, object>();
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Key)))
        {
            parameters.Add("Key", Key);
        }

        if (MyInvocation.BoundParameters.ContainsKey(nameof(ConfiguredOnly)))
        {
            parameters.Add("ConfiguredOnly", ConfiguredOnly.IsPresent);
        }

        ExecuteInvocation(parameters);
    }
}
