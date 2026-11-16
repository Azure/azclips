// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    /// <summary>
    /// Cancels a running subscription-scoped deployment
    /// (POST /subscriptions/{sub}/providers/Microsoft.Resources/deployments/{name}/cancel).
    /// ARM answers 204 No Content, so nothing is written on success.
    /// </summary>
    public class StopSubscriptionDeploymentProcessor : ResourcesProcessorBase<object, object>
    {
        private const string ApiVersion = "2025-04-01";

        public string SubscriptionId { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        protected override HttpMethod Method => HttpMethod.Post;

        protected override string CreateRequestUri() =>
            $"https://management.azure.com/subscriptions/{Uri.EscapeDataString(SubscriptionId)}/providers/Microsoft.Resources/deployments/{Uri.EscapeDataString(Name)}/cancel?api-version={ApiVersion}";
    }
}
