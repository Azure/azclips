// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Json;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Cirrus.ExtensionRuntime;
using Microsoft.Azure.Cirrus.Extensions.Resources.Models;

namespace Microsoft.Azure.Cirrus.Extensions.Resources.Processors
{
    /// <summary>
    /// Atomic processor: PUTs a resource-group-scoped deployment
    /// (PUT .../resourcegroups/{rg}/providers/Microsoft.Resources/deployments/{name}) and waits for
    /// the long-running operation to reach a terminal provisioningState. Pure transport: it
    /// serializes the already-built <see cref="DeploymentProperties"/> and does no validation,
    /// file IO, or bicep compilation. All of that (and the full user-facing argument surface) lives
    /// in <see cref="NewResourceGroupDeploymentProcessor_Orchestrator"/>, which builds the
    /// properties and delegates here via <c>CreateExtendedProcessor</c>.
    /// </summary>
    public class NewResourceGroupDeploymentProcessor : ResourcesProcessorBase<DeploymentExtended>
    {
        private const string ApiVersion = "2025-04-01";

        public string ResourceGroupName { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public DeploymentProperties Properties { get; set; } = new();

        // ARM resource-group deployments are long-running (final-state-via: azure-async-operation).
        // The deployment resource itself carries provisioningState, so the async-operation/location
        // headers drive polling and a final GET on the deployment URI (the default _finalUri) yields
        // the terminal DeploymentExtended. GetFinalUri/GetOperationLocationUri stay null (default).
        protected override Func<HttpResponseMessage, string?> GetAsyncOperationUri => response => response.GetFirstHeader("Azure-AsyncOperation");
        protected override Func<HttpResponseMessage, string?> GetLocationUri => response => response.GetFirstHeader("Location");

        // Always waits for the deployment to reach a terminal provisioningState by polling
        // (matches `az deployment group create`). SubscriptionId is resolved here so the
        // deployment URI is built correctly.
        internal override Task<HttpResponseMessage> SendAsync()
        {
            ResolveSubscriptionId();
            return SendAsyncLRO();
        }

        protected override async Task<HttpRequestMessage> CreateRequestMessage()
        {
            await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);

            Deployment body = new Deployment { Properties = Properties };

            string uri = $"https://management.azure.com/subscriptions/{Uri.EscapeDataString(SubscriptionId)}/resourcegroups/{Uri.EscapeDataString(ResourceGroupName)}/providers/Microsoft.Resources/deployments/{Uri.EscapeDataString(Name)}?api-version={ApiVersion}";
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, uri)
            {
                Content = JsonContent.Create(body, options: JsonOptions)
            };

            await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
            return request;
        }
    }
}
