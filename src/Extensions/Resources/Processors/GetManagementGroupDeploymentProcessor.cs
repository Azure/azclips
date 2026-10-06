// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    /// <summary>
    /// Atomic processor: GETs a single management-group-scoped deployment
    /// (GET /providers/Microsoft.Management/managementGroups/{mg}/providers/Microsoft.Resources/deployments/{name}).
    /// </summary>
    public class GetManagementGroupDeploymentProcessor : ResourcesProcessorBase<object, DeploymentExtended>
    {
        private const string ApiVersion = "2026-06-01";

        public string ManagementGroupId { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        protected override HttpMethod Method => HttpMethod.Get;

        protected override string CreateRequestUri() =>
            $"https://management.azure.com/providers/Microsoft.Management/managementGroups/{Uri.EscapeDataString(ManagementGroupId)}/providers/Microsoft.Resources/deployments/{Uri.EscapeDataString(Name)}?api-version={ApiVersion}";
    }
}
