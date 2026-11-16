// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Cmdlets.Resources;

[Cmdlet(VerbsCommon.Get, "AzTenantDeploymentWhatIfResult")]
[OutputType(typeof(WhatIfOperationResult))]
public class GetTenantDeploymentWhatIfResultCmdlet : TenantDeploymentTemplateCmdletBase
{
    // The PowerShell layer does not enforce the command metadata's AllowedValues, so reject other values here.
    [Parameter(Mandatory = false, HelpMessage = "The format of the what-if results. FullResourcePayloads (default) or ResourceIdOnly.")]
    [ValidateSet("FullResourcePayloads", "ResourceIdOnly", IgnoreCase = true)]
    public string? ResultFormat { get; set; }

    protected override void ProcessRecord() => Invoke("Get-AzTenantDeploymentWhatIfResult", GetBoundParameters());
}
