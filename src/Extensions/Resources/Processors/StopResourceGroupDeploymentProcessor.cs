// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    /// <summary>
    /// Cancels a running resource-group-scoped deployment
    /// (POST .../resourcegroups/{rg}/providers/Microsoft.Resources/deployments/{name}/cancel).
    /// ARM answers 204 No Content, so this processor writes no output on success.
    /// </summary>
    public class StopResourceGroupDeploymentProcessor : ResourcesProcessorBase<object, object>
    {
        private const string ApiVersion = "2025-04-01";

        public string SubscriptionId { get; set; } = string.Empty;

        public string ResourceGroupName { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        protected override HttpMethod Method => HttpMethod.Post;

        protected override string CreateRequestUri() =>
            $"https://management.azure.com/subscriptions/{Uri.EscapeDataString(SubscriptionIdValidator.Require(SubscriptionId))}/resourcegroups/{Uri.EscapeDataString(ResourceGroupName)}/providers/Microsoft.Resources/deployments/{Uri.EscapeDataString(Name)}/cancel?api-version={ApiVersion}";
    }
}
