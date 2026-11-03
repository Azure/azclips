// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    /// <summary>
    /// Atomic processor: PUTs a subscription-scoped deployment
    /// (PUT /subscriptions/{sub}/providers/Microsoft.Resources/deployments/{name}) and waits for the
    /// long-running operation to finish. Pure transport; inputs are built by
    /// <see cref="NewSubscriptionDeploymentProcessor_Orchestrator"/>.
    /// </summary>
    public class NewSubscriptionDeploymentProcessor : ResourcesProcessorBase<Deployment, DeploymentExtended>
    {
        private const string ApiVersion = "2025-04-01";

        public string SubscriptionId { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Location { get; set; } = string.Empty;

        public DeploymentProperties Properties { get; set; } = new();

        // Same polling as the resource-group variant: async-operation/location headers drive the
        // poll, then the default final GET on the deployment URI returns the DeploymentExtended.
        protected override Func<HttpResponseMessage, string?> GetAsyncOperationUri => response => response.GetFirstHeader("Azure-AsyncOperation");
        protected override Func<HttpResponseMessage, string?> GetLocationUri => response => response.GetFirstHeader("Location");

        internal override Task<HttpResponseMessage> SendAsync() => SendAsyncLRO();

        protected override HttpMethod Method => HttpMethod.Put;

        protected override string CreateRequestUri() =>
            $"https://management.azure.com/subscriptions/{Uri.EscapeDataString(SubscriptionId)}/providers/Microsoft.Resources/deployments/{Uri.EscapeDataString(Name)}?api-version={ApiVersion}";

        protected override Deployment? CreateRequestBody() => new Deployment { Location = Location, Properties = Properties };
    }
}
