// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using System.Net.Http.Json;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
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
    public class NewResourceGroupDeploymentProcessor : ResourcesProcessorBase<Deployment, DeploymentExtended>
    {
        private const string ApiVersion = "2025-04-01";

        public string SubscriptionId { get; set; } = string.Empty;

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
        // (matches `az deployment group create`).
        internal override Task<HttpResponseMessage> SendAsync() => SendAsyncLRO();

        protected override HttpMethod Method => HttpMethod.Put;

        protected override string CreateRequestUri() =>
            $"https://management.azure.com/subscriptions/{Uri.EscapeDataString(SubscriptionIdValidator.Require(SubscriptionId))}/resourcegroups/{Uri.EscapeDataString(ResourceGroupName)}/providers/Microsoft.Resources/deployments/{Uri.EscapeDataString(Name)}?api-version={ApiVersion}";

        protected override Deployment? CreateRequestBody() => new Deployment { Properties = Properties };
    }
}
