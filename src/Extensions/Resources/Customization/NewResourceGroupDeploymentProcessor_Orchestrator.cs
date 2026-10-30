// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    /// <summary>
    /// Creates or updates a deployment at the resource group scope
    /// (PUT .../resourcegroups/{rg}/providers/Microsoft.Resources/deployments/{name}).
    /// This is the AzClips equivalent of <c>az deployment group create</c> /
    /// <c>New-AzResourceGroupDeployment</c>.
    /// <para>
    /// Orchestrator (handwritten/customized) half of the deployment-create command: it owns the
    /// create-specific argument surface (rollback) on top of the shared template-source,
    /// parameters-source, mode, inline parameters, and validation-level options inherited from
    /// <see cref="DeploymentOrchestratorBase{TRequest, TResponse}"/>, together with the validation and the
    /// template/parameters/bicep building. It builds the request body and delegates the actual
    /// PUT + long-running-operation transport to the atomic
    /// <see cref="NewResourceGroupDeploymentProcessor"/> via <c>CreateExtendedProcessor</c>.
    /// </para>
    /// </summary>
    public class NewResourceGroupDeploymentProcessor_Orchestrator : DeploymentOrchestratorBase<Deployment, DeploymentExtended>
    {
        public string SubscriptionId { get; set; } = string.Empty;

        public string ResourceGroupName { get; set; } = string.Empty;

        public bool RollbackOnError { get; set; }

        public string? RollbackToDeploymentName { get; set; }

        // Builds the deployment request body (after validating the inputs) and delegates the PUT +
        // long-running-operation wait to the atomic NewResourceGroupDeploymentProcessor. The
        // deployment always waits for a terminal provisioningState (matches `az deployment group
        // create`); there is no no-wait branch.
        internal override async Task<HttpResponseMessage> SendAsync()
        {
            ValidateInputs();
            await DownloadRemoteInputsAsync();

            DeploymentProperties properties = new DeploymentProperties
            {
                Mode = ParseMode(Mode),
                Template = BuildTemplate(out TemplateLink? templateLink),
                TemplateLink = templateLink,
                Parameters = BuildParameters(out ParametersLink? parametersLink),
                ParametersLink = parametersLink,
                OnErrorDeployment = BuildOnErrorDeployment(),
                ValidationLevel = NormalizeValidationLevel(ValidationLevel),
            };

            await ValidateDeployment(properties);

            NewResourceGroupDeploymentProcessor processor = CreateExtendedProcessor<NewResourceGroupDeploymentProcessor>(p =>
            {
                p.SubscriptionId = SubscriptionId;
                p.ResourceGroupName = ResourceGroupName;
                p.Name = Name;
                p.Properties = properties;
            });

            return await processor.SendAsync();
        }

        // Preflight validation, matching legacy clients (az: begin_validate; Az PowerShell: RunDeploymentValidation):
        // an invalid template fails here without issuing the PUT or leaving a failed deployment record.
        private async Task ValidateDeployment(DeploymentProperties properties)
        {
            TestResourceGroupDeploymentProcessor validator = CreateExtendedProcessor<TestResourceGroupDeploymentProcessor>(p =>
            {
                p.SubscriptionId = SubscriptionId;
                p.ResourceGroupName = ResourceGroupName;
                p.Name = Name;
                p.Properties = properties;
            });

            using HttpResponseMessage response = await validator.SendAsync();
            string body = response.Content is null ? string.Empty : await response.Content.ReadAsStringAsync();
            if (!string.IsNullOrWhiteSpace(body)
                && JsonSerializer.Deserialize<DeploymentValidateResult>(body, JsonOptions)?.Error is { } error)
            {
                throw new InvalidOperationException(BuildErrorMessage("Deployment validation failed.", error));
            }

            if (!response.IsSuccessStatusCode)
            {
                await response.EnsureSuccessAsync(Publisher);
            }
        }

        // Validates the shared template/parameters/mode/inline/validation-level rules together with
        // the create-specific rollback rule. Fails fast with deterministic ArgumentExceptions.
        private void ValidateInputs()
        {
            ValidateResourceGroupName(ResourceGroupName);
            ValidateCommonInputs();

            if (RollbackOnError && !string.IsNullOrWhiteSpace(RollbackToDeploymentName))
            {
                throw AzclipsErrorException.InvalidInput("Provide at most one of -RollbackOnError or -RollbackToDeploymentName; they are mutually exclusive.");
            }
        }

        private OnErrorDeployment? BuildOnErrorDeployment()
        {
            if (!string.IsNullOrWhiteSpace(RollbackToDeploymentName))
            {
                return new OnErrorDeployment
                {
                    Type = "SpecificDeployment",
                    DeploymentName = RollbackToDeploymentName,
                };
            }

            if (RollbackOnError)
            {
                return new OnErrorDeployment { Type = "LastSuccessful" };
            }

            return null;
        }
    }
}
