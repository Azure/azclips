// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Cmdlets.Resources;

[Cmdlet(VerbsDiagnostic.Test, "AzTenantDeployment")]
[OutputType(typeof(DeploymentValidateResult))]
public class TestTenantDeploymentCmdlet : TenantDeploymentTemplateCmdletBase
{
    protected override void ProcessRecord() => Invoke("Test-AzTenantDeployment", GetBoundParameters());
}
