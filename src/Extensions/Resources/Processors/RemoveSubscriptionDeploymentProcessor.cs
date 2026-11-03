// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    /// <summary>
    /// Deletes a subscription-scoped deployment from the deployment history
    /// (DELETE /subscriptions/{sub}/providers/Microsoft.Resources/deployments/{name}). Does not
    /// affect the resources the deployment created. Long-running (202 + Location, then 204).
    /// </summary>
    public class RemoveSubscriptionDeploymentProcessor : ResourcesProcessorBase<object, object>
    {
        private const string ApiVersion = "2025-04-01";

        public string SubscriptionId { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        // No final GET: the Location poll already ends in 204, and a GET on the deleted deployment would 404.
        protected override Func<HttpResponseMessage, string?> GetFinalUri => _ => null;
        protected override Func<HttpResponseMessage, string?> GetOperationLocationUri => response => response.GetFirstHeader("Operation-Location");
        protected override Func<HttpResponseMessage, string?> GetAsyncOperationUri => response => response.GetFirstHeader("Azure-AsyncOperation");
        protected override Func<HttpResponseMessage, string?> GetLocationUri => response => response.GetFirstHeader("Location");

        protected override Task<bool> BeforeWriteOutputAsyncAndReturn(object? body) => Task.FromResult(true);

        internal override Task<HttpResponseMessage> SendAsync() => SendAsyncLRO();

        protected override HttpMethod Method => HttpMethod.Delete;

        protected override string CreateRequestUri() =>
            $"https://management.azure.com/subscriptions/{Uri.EscapeDataString(SubscriptionId)}/providers/Microsoft.Resources/deployments/{Uri.EscapeDataString(Name)}?api-version={ApiVersion}";
    }
}
