// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    /// <summary>
    /// Customization: GETs a management-group-scoped deployment addressed by its fully qualified resource id.
    /// Backs the PowerShell <c>-Id</c> parameter set of <c>Get-AzManagementGroupDeployment</c>; both the
    /// management group id and the deployment name are computed from <see cref="Id"/>.
    /// </summary>
    public class GetManagementGroupDeploymentProcessor_ById : ResourcesProcessorBase<object, DeploymentExtended>
    {
        private const string ApiVersion = "2026-06-01";

        public string Id { get; set; } = string.Empty;

        protected override HttpMethod Method => HttpMethod.Get;

        protected override string CreateRequestUri()
        {
            (string managementGroupId, string deploymentName) = DeploymentIdResolver.ParseManagementGroupScope(Id);
            return $"https://management.azure.com/providers/Microsoft.Management/managementGroups/{Uri.EscapeDataString(managementGroupId)}/providers/Microsoft.Resources/deployments/{Uri.EscapeDataString(deploymentName)}?api-version={ApiVersion}";
        }
    }
}
