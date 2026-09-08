// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Json;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    /// <summary>
    /// Atomic processor: runs an ARM what-if at the resource group scope
    /// (POST .../resourcegroups/{rg}/providers/Microsoft.Resources/deployments/{name}/whatIf) and
    /// waits for the long-running operation to reach a terminal status, returning the predicted
    /// <see cref="WhatIfOperationResult"/> change list. Pure transport: it serializes the
    /// already-built <see cref="DeploymentWhatIf"/> body and does no validation, file IO, or bicep
    /// compilation. All of that (and the full user-facing argument surface) lives in
    /// <see cref="NewResourceGroupDeploymentWhatIfProcessor_Orchestrator"/>, which builds the body
    /// and delegates here via <c>CreateExtendedProcessor</c>. What-if is read-only: it never
    /// creates or modifies resources.
    /// </summary>
    public class NewResourceGroupDeploymentWhatIfProcessor : ResourcesProcessorBase<DeploymentWhatIf, WhatIfOperationResult>
    {
        private const string ApiVersion = "2025-04-01";

        public string SubscriptionId { get; set; } = string.Empty;

        public string ResourceGroupName { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public DeploymentWhatIf WhatIf { get; set; } = new();

        // The what-if operation is long-running: the initial POST returns 202 + Location + Retry-After,
        // and polling the Location header returns 200 with the full WhatIfOperationResult body. The
        // Azure-AsyncOperation header (if present) only carries a bare {status} WITHOUT the changes
        // payload, so it is intentionally NOT wired here (GetAsyncOperationUri stays null) — otherwise
        // the poller would prefer it and the terminal response would lose the change list. The
        // original POST /whatIf URI is POST-only, so the final GET is skipped (FetchFinalState=false)
        // and the last Location poll (the 200 carrying the result body) is returned and deserialized.
        protected override Func<HttpResponseMessage, string?> GetLocationUri => response => response.GetFirstHeader("Location");

        // What-if always waits for the LRO to complete (there is no -NoWait).
        internal override Task<HttpResponseMessage> SendAsync() => SendAsyncLRO(fetchFinalState: false);

        protected override HttpMethod Method => HttpMethod.Post;

        protected override string CreateRequestUri() =>
            $"https://management.azure.com/subscriptions/{Uri.EscapeDataString(SubscriptionId)}/resourcegroups/{Uri.EscapeDataString(ResourceGroupName)}/providers/Microsoft.Resources/deployments/{Uri.EscapeDataString(Name)}/whatIf?api-version={ApiVersion}";

        protected override DeploymentWhatIf? CreateRequestBody() => WhatIf;
    }
}
