// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    /// <summary>
    /// Atomic processor: GETs a single operation of a subscription-scoped deployment.
    /// </summary>
    public class GetSubscriptionDeploymentOperationProcessor : ResourcesProcessorBase<object, DeploymentOperation>
    {
        private const string ApiVersion = "2026-06-01";

        public string SubscriptionId { get; set; } = string.Empty;

        public string DeploymentName { get; set; } = string.Empty;

        public string OperationId { get; set; } = string.Empty;

        protected override HttpMethod Method => HttpMethod.Get;

        protected override string CreateRequestUri() =>
            $"https://management.azure.com/subscriptions/{Uri.EscapeDataString(SubscriptionId)}/providers/Microsoft.Resources/deployments/{Uri.EscapeDataString(DeploymentName)}/operations/{Uri.EscapeDataString(OperationId)}?api-version={ApiVersion}";
    }
}
