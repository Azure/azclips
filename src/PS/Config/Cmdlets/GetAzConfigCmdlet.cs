// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;

namespace Microsoft.Azure.Azclips.Cmdlets.Config;

[Cmdlet(VerbsCommon.Get, "AzConfig")]
[OutputType("Microsoft.Azure.Azclips.Extensions.Config.Models.ConfigEntry")]
public class GetAzConfigCmdlet : ConfigCmdletBase
{
    protected override bool EnumerateOutput => true;

    [Parameter(Mandatory = true)]
    public string Key { get; set; } = string.Empty;

    protected override void ProcessRecord()
    {

        var parameters = new Dictionary<string, object>();
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Key)))
        {
            parameters.Add("Key", Key);
        }

        ExecuteInvocation(parameters);
    }
}
