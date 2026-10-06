// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    /// <summary>
    /// Customization: GETs a subscription-scoped deployment addressed by its fully qualified resource id.
    /// Backs the PowerShell <c>-Id</c> parameter set of <c>Get-AzDeployment</c>.
    /// </summary>
    public class GetSubscriptionDeploymentProcessor_ById : ResourcesProcessorBase<object, DeploymentExtended>
    {
        private const string ApiVersion = "2026-06-01";

        public string SubscriptionId { get; set; } = string.Empty;

        public string Id { get; set; } = string.Empty;

        protected override HttpMethod Method => HttpMethod.Get;

        protected override string CreateRequestUri()
        {
            string deploymentName = DeploymentIdResolver.ParseDeploymentName(Id);
            return $"https://management.azure.com/subscriptions/{Uri.EscapeDataString(SubscriptionId)}/providers/Microsoft.Resources/deployments/{Uri.EscapeDataString(deploymentName)}?api-version={ApiVersion}";
        }
    }
}
