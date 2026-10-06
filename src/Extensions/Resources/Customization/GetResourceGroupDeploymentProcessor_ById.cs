// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    /// <summary>
    /// Customization: GETs a resource-group-scoped deployment addressed by its fully qualified resource id
    /// instead of by resource group + name. Backs the PowerShell <c>-Id</c> parameter set of
    /// <c>Get-AzResourceGroupDeployment</c>, which has no Azure CLI equivalent. The resource group and
    /// deployment name are computed from <see cref="Id"/>; the request is otherwise identical to
    /// <see cref="GetResourceGroupDeploymentProcessor"/>.
    /// </summary>
    public class GetResourceGroupDeploymentProcessor_ById : ResourcesProcessorBase<object, DeploymentExtended>
    {
        private const string ApiVersion = "2026-06-01";

        public string SubscriptionId { get; set; } = string.Empty;

        public string Id { get; set; } = string.Empty;

        protected override HttpMethod Method => HttpMethod.Get;

        protected override string CreateRequestUri()
        {
            (string resourceGroupName, string deploymentName) = DeploymentIdResolver.ParseResourceGroupScope(Id);
            return $"https://management.azure.com/subscriptions/{Uri.EscapeDataString(SubscriptionId)}/resourcegroups/{Uri.EscapeDataString(resourceGroupName)}/providers/Microsoft.Resources/deployments/{Uri.EscapeDataString(deploymentName)}?api-version={ApiVersion}";
        }
    }
}
