// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Cmdlets.Resources;

[Cmdlet(VerbsCommon.New, "AzTenantDeployment", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
[OutputType(typeof(DeploymentExtended))]
public class NewTenantDeploymentCmdlet : TenantDeploymentTemplateCmdletBase
{
    [Parameter(Mandatory = false)]
    public string Name { get; set; } = "";

    [Parameter(Mandatory = false)]
    public string? ParametersUri { get; set; }

    [Parameter(Mandatory = false)]
    public string? ParametersFile { get; set; }

    [Parameter(Mandatory = false)]
    public string? WhatIfResultFormat { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? WhatIfExcludeChangeType { get; set; }

    // -WhatIf runs only the what-if and writes its change report, like az deployment tenant create --what-if.
    protected override void ProcessRecord()
    {
        Dictionary<string, object> parameters = GetBoundParameters();
        if (IsWhatIfRequested())
        {
            parameters["WhatIf"] = true;
            Invoke("New-AzTenantDeployment", parameters);
        }
        else if (ShouldProcess(string.IsNullOrEmpty(Name) ? "deployment at tenant scope" : $"deployment '{Name}' at tenant scope", "Create"))
        {
            Invoke("New-AzTenantDeployment", parameters);
        }
    }

    private bool IsWhatIfRequested() =>
        MyInvocation.BoundParameters.TryGetValue("WhatIf", out object? value)
            ? ((SwitchParameter)value).IsPresent
            : LanguagePrimitives.IsTrue(SessionState?.PSVariable.GetValue("WhatIfPreference"));
}
