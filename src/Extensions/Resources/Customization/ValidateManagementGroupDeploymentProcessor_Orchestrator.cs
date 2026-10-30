// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// <c>az deployment mg validate</c> / <c>Test-AzManagementGroupDeployment</c>: POST validate, throw on a template error,
/// otherwise write the result, like <see cref="TestResourceGroupDeploymentProcessor_Orchestrator"/>.
/// </summary>
public class ValidateManagementGroupDeploymentProcessor_Orchestrator : ManagementGroupDeploymentOrchestratorBase<ScopedDeployment, DeploymentValidateResult>
{
    internal override async Task<HttpResponseMessage> SendAsync() =>
        await ValidateAsync(new ScopedDeployment { Location = Location, Properties = await BuildPropertiesAsync() });
}
