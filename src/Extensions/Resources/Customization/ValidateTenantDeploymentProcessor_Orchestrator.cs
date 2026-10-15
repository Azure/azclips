// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// <c>az deployment tenant validate</c> / <c>Test-AzTenantDeployment</c>: POST validate, throw on a template error,
/// otherwise write the result, like <see cref="TestResourceGroupDeploymentProcessor_Orchestrator"/>.
/// </summary>
public class ValidateTenantDeploymentProcessor_Orchestrator : TenantDeploymentOrchestratorBase<ScopedDeployment, DeploymentValidateResult>
{
    internal override Task<HttpResponseMessage> SendAsync() =>
        ValidateAsync(new ScopedDeployment { Location = Location, Properties = BuildProperties() });
}
