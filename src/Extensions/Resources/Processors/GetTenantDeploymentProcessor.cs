// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    /// <summary>
    /// Atomic processor: GETs a single tenant-scoped deployment
    /// (GET /providers/Microsoft.Resources/deployments/{name}).
    /// </summary>
    public class GetTenantDeploymentProcessor : ResourcesProcessorBase<object, DeploymentExtended>
    {
        private const string ApiVersion = "2026-06-01";

        public string Name { get; set; } = string.Empty;

        protected override HttpMethod Method => HttpMethod.Get;

        protected override string CreateRequestUri() =>
            $"https://management.azure.com/providers/Microsoft.Resources/deployments/{Uri.EscapeDataString(Name)}?api-version={ApiVersion}";
    }
}
