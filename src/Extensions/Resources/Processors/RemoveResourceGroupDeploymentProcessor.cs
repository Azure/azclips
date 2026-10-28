// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    /// <summary>
    /// Deletes a resource-group-scoped deployment from the deployment history
    /// (DELETE .../resourcegroups/{rg}/providers/Microsoft.Resources/deployments/{name}).
    /// Removing the deployment record also removes its deployment operations; it does not affect
    /// the resources the deployment created.
    /// </summary>
    /// <remarks>
    /// The operation is long-running (<c>final-state-via: location</c>): ARM answers 202 with a
    /// Location header, that URI keeps answering 202 while the delete runs, and answers 204 when it
    /// finishes. A deployment that does not exist is answered 204 straight away, so the polling loop
    /// is never entered and the command silently succeeds. Either way the command emits nothing.
    /// </remarks>
    public class RemoveResourceGroupDeploymentProcessor : ResourcesProcessorBase<object, object>
    {
        private const string ApiVersion = "2025-04-01";

        public string SubscriptionId { get; set; } = string.Empty;

        public string ResourceGroupName { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        // No final URI: the Location poll already ends in 204 when the delete has completed, so the
        // extra final GET has nothing left to fetch. Pointing it at the deleted deployment would 404
        // a successful delete, and pointing it at the Location URI would just repeat the last poll.
        protected override Func<HttpResponseMessage, string?> GetFinalUri => _ => null;
        protected override Func<HttpResponseMessage, string?> GetOperationLocationUri => response => response.GetFirstHeader("Operation-Location");
        protected override Func<HttpResponseMessage, string?> GetAsyncOperationUri => response => response.GetFirstHeader("Azure-AsyncOperation");
        protected override Func<HttpResponseMessage, string?> GetLocationUri => response => response.GetFirstHeader("Location");

        // Delete emits nothing even when the terminal poll answers 200 with a status document.
        protected override Task<bool> BeforeWriteOutputAsyncAndReturn(object? body) => Task.FromResult(true);

        // Always waits for the delete to finish (matches `az deployment group delete`).
        internal override Task<HttpResponseMessage> SendAsync() => SendAsyncLRO();

        protected override HttpMethod Method => HttpMethod.Delete;

        protected override string CreateRequestUri() =>
            $"https://management.azure.com/subscriptions/{Uri.EscapeDataString(SubscriptionIdValidator.Require(SubscriptionId))}/resourcegroups/{Uri.EscapeDataString(ResourceGroupName)}/providers/Microsoft.Resources/deployments/{Uri.EscapeDataString(Name)}?api-version={ApiVersion}";
    }
}
