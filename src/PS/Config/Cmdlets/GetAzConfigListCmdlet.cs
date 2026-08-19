// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;

namespace Microsoft.Azure.Azclips.Cmdlets.Config;

[Cmdlet(VerbsCommon.Get, "AzConfigList")]
[OutputType(typeof(string))]
public class GetAzConfigListCmdlet : ConfigCmdletBase
{
    [Parameter(Mandatory = false)]
    public SwitchParameter IncludeDefaults { get; set; }

    [Parameter(Mandatory = false)]
    public SwitchParameter AsJson { get; set; }

    protected override void ProcessRecord()
    {

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
