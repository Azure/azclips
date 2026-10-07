// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;

namespace Microsoft.Azure.Azclips.Cmdlets.Config;

[Cmdlet(VerbsCommon.Get, "AzConfigList")]
[OutputType("Microsoft.Azure.Azclips.Extensions.Config.Models.ConfigEntry")]
public class GetAzConfigListCmdlet : ConfigCmdletBase
{
    protected override bool EnumerateOutput => true;

    [Parameter(Mandatory = false)]
    public SwitchParameter IncludeDefaults { get; set; }

    protected override void ProcessRecord()
    {

        var parameters = new Dictionary<string, object>();
        if (MyInvocation.BoundParameters.ContainsKey(nameof(IncludeDefaults)))
        {
            parameters.Add("All", IncludeDefaults.IsPresent);
        }

        ExecuteInvocation(parameters);
    }
}
