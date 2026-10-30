// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// <c>az deployment tenant validate</c>: POST validate, throw on a template error, otherwise write the result,
/// like <see cref="TestResourceGroupDeploymentProcessor_Orchestrator"/>.
/// <c>Test-AzTenantDeployment</c> uses <see cref="ValidateTenantDeploymentProcessor_PSOutput"/> instead.
/// </summary>
public class ValidateTenantDeploymentProcessor_Orchestrator : TenantDeploymentOrchestratorBase<ScopedDeployment, DeploymentValidateResult>
{
    internal override Task<HttpResponseMessage> SendAsync() => ValidateAsync(BuildDeployment());

    protected ScopedDeployment BuildDeployment() => new() { Location = Location, Properties = BuildProperties() };
}