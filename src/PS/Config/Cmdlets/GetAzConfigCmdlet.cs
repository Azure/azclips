// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;

namespace Microsoft.Azure.Azclips.Cmdlets.Config;

[Cmdlet(VerbsCommon.Get, "AzConfig")]
[OutputType(typeof(string))]
public class GetAzConfigCmdlet : ConfigCmdletBase
{
    [Parameter(Mandatory = true)]
    public string Key { get; set; } = string.Empty;

    [Parameter(Mandatory = false)]
    public SwitchParameter AsJson { get; set; }

    protected override void ProcessRecord()
    {

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
