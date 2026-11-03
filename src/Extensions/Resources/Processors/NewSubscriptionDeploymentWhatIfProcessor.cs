// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    /// <summary>
    /// Atomic processor: runs an ARM what-if at subscription scope
    /// (POST /subscriptions/{sub}/providers/Microsoft.Resources/deployments/{name}/whatIf) and waits
    /// for the predicted <see cref="WhatIfOperationResult"/>. Pure transport; the body is built by
    /// <see cref="NewSubscriptionDeploymentWhatIfProcessor_Orchestrator"/>.
    /// </summary>
    public class NewSubscriptionDeploymentWhatIfProcessor : ResourcesProcessorBase<DeploymentWhatIf, WhatIfOperationResult>
    {
        private const string ApiVersion = "2025-04-01";

        public string SubscriptionId { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public DeploymentWhatIf WhatIf { get; set; } = new();

        // Same as the resource-group variant: only Location is wired, because Azure-AsyncOperation
        // carries a bare {status} without the change list and the POST URI cannot be re-fetched.
        protected override Func<HttpResponseMessage, string?> GetLocationUri => response => response.GetFirstHeader("Location");

        internal override Task<HttpResponseMessage> SendAsync() => SendAsyncLRO();

        protected override HttpMethod Method => HttpMethod.Post;

        protected override string CreateRequestUri() =>
            $"https://management.azure.com/subscriptions/{Uri.EscapeDataString(SubscriptionId)}/providers/Microsoft.Resources/deployments/{Uri.EscapeDataString(Name)}/whatIf?api-version={ApiVersion}";

        protected override DeploymentWhatIf? CreateRequestBody() => WhatIf;
    }
}
