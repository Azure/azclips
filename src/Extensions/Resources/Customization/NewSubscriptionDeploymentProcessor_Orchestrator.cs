// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    /// <summary>
    /// Creates or updates a deployment at subscription scope
    /// (PUT /subscriptions/{sub}/providers/Microsoft.Resources/deployments/{name}).
    /// AzClips equivalent of <c>az deployment sub create</c> / <c>New-AzDeployment</c>.
    /// Builds the request from the shared <see cref="DeploymentOrchestratorBase{TRequest, TResponse}"/>
    /// inputs plus the required <see cref="Location"/>, and delegates the PUT + long-running-operation
    /// wait to <see cref="NewSubscriptionDeploymentProcessor"/>.
    /// </summary>
    public class NewSubscriptionDeploymentProcessor_Orchestrator : DeploymentOrchestratorBase<Deployment, DeploymentExtended>
    {
        public string SubscriptionId { get; set; } = string.Empty;

        /// <summary>Where ARM stores the deployment data; required at subscription scope.</summary>
        public string Location { get; set; } = string.Empty;

        internal override async Task<HttpResponseMessage> SendAsync()
        {
            ValidateLocation(Location);
            ValidateCommonInputs();

            DeploymentProperties properties = new DeploymentProperties
            {
                Template = BuildTemplate(out TemplateLink? templateLink),
                TemplateLink = templateLink,
                Parameters = BuildParameters(out ParametersLink? parametersLink),
                ParametersLink = parametersLink,
                ValidationLevel = NormalizeValidationLevel(ValidationLevel),
            };

            NewSubscriptionDeploymentProcessor processor = CreateExtendedProcessor<NewSubscriptionDeploymentProcessor>(p =>
            {
                p.SubscriptionId = SubscriptionId;
                p.Name = Name;
                p.Location = Location;
                p.Properties = properties;
            });

            return await processor.SendAsync();
        }
    }
}
