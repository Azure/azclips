// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    /// <summary>
    /// Atomic processor: GETs a single subscription-scoped deployment
    /// (GET /subscriptions/{sub}/providers/Microsoft.Resources/deployments/{name}).
    /// </summary>
    public class GetSubscriptionDeploymentProcessor : ResourcesProcessorBase<object, DeploymentExtended>
    {
        private const string ApiVersion = "2026-06-01";

        public string SubscriptionId { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        protected override HttpMethod Method => HttpMethod.Get;

        protected override string CreateRequestUri() =>
            $"https://management.azure.com/subscriptions/{Uri.EscapeDataString(SubscriptionId)}/providers/Microsoft.Resources/deployments/{Uri.EscapeDataString(Name)}?api-version={ApiVersion}";
    }
}
