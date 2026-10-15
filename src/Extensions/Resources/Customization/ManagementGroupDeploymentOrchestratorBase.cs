// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// Management group scope for <c>az deployment mg create|validate|what-if</c> and <c>New-/Test-AzManagementGroupDeployment</c>:
/// reuses the resource group input surface of <see cref="DeploymentOrchestratorBase{TRequest, TResponse}"/>, adds
/// ManagementGroupId and Location, and skips the resource group check.
/// </summary>
public abstract class ManagementGroupDeploymentOrchestratorBase<TRequest, TResponse> : DeploymentOrchestratorBase<TRequest, TResponse>
    where TRequest : class
    where TResponse : class
{
    public string ManagementGroupId { get; set; } = string.Empty;

    public string Location { get; set; } = string.Empty;

    protected override bool RequiresResourceGroup => false;

    protected DeploymentProperties BuildProperties()
    {
        if (string.IsNullOrWhiteSpace(ManagementGroupId))
        {
            throw AzclipsErrorException.InvalidInput("ManagementGroupId must be provided.");
        }
        if (string.IsNullOrWhiteSpace(Location))
        {
            throw AzclipsErrorException.InvalidInput("Location must be provided.");
        }
        ValidateCommonInputs();

        return new DeploymentProperties
        {
            Mode = ParseMode(Mode),
            Template = BuildTemplate(out TemplateLink? templateLink),
            TemplateLink = templateLink,
            Parameters = BuildParameters(out ParametersLink? parametersLink),
            ParametersLink = parametersLink,
            ValidationLevel = NormalizeValidationLevel(ValidationLevel),
        };
    }

    // POST validate; a template error throws like Test-AzResourceGroupDeployment.
    protected async Task<HttpResponseMessage> ValidateAsync(ScopedDeployment deployment)
    {
        HttpResponseMessage response = await CreateExtendedProcessor<ValidateAtManagementGroupScopeDeploymentsProcessor>(p =>
        {
            p.GroupId = ManagementGroupId;
            p.DeploymentName = Name;
            p.Body = deployment;
        }).SendAsyncAllowBadRequest();

        string body = response.Content is null ? string.Empty : await response.Content.ReadAsStringAsync();
        if (!string.IsNullOrWhiteSpace(body) && JsonSerializer.Deserialize<DeploymentValidateResult>(body, JsonOptions)?.Error is { } error)
        {
            response.Dispose();
            throw new InvalidOperationException(TestResourceGroupDeploymentProcessor_Orchestrator.BuildErrorMessage(error));
        }
        if (!response.IsSuccessStatusCode)
        {
            await response.EnsureSuccessAsync(Publisher);
        }
        return response;
    }
}
