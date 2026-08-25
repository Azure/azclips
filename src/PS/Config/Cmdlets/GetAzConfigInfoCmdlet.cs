// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;

namespace Microsoft.Azure.Azclips.Cmdlets.Config;

[Cmdlet(VerbsCommon.Get, "AzConfigInfo")]
[OutputType(typeof(string))]
public class GetAzConfigInfoCmdlet : ConfigCmdletBase
{
    [Parameter(Mandatory = false)]
    public SwitchParameter KeysOnly { get; set; }

    protected override void ProcessRecord()
    {

        var parameters = new Dictionary<string, object>();
        if (MyInvocation.BoundParameters.ContainsKey(nameof(KeysOnly)))
        {
            parameters.Add("KeysOnly", KeysOnly.IsPresent);
        }

        ExecuteInvocation(parameters);
    }
}
