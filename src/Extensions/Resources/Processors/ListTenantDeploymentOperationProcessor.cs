// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    /// <summary>
    /// Atomic processor: lists all operations of a tenant-scoped deployment, draining every page.
    /// </summary>
    public class ListTenantDeploymentOperationProcessor : ResourcesProcessorBase<object, DeploymentOperationsListResult>
    {
        private const string ApiVersion = "2026-06-01";

        public string DeploymentName { get; set; } = string.Empty;

        protected override HttpMethod Method => HttpMethod.Get;

        protected override string CreateRequestUri() =>
            $"https://management.azure.com/providers/Microsoft.Resources/deployments/{Uri.EscapeDataString(DeploymentName)}/operations?api-version={ApiVersion}";

        private string? nextLink(DeploymentOperationsListResult body) => body.NextLink;
        private List<DeploymentOperation>? pageItems(DeploymentOperationsListResult body) => body.Value;

        protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(DeploymentOperationsListResult? body)
        {
            await body.WriteOutputWithPaginationAsync(pageItems, nextLink, CreateRequestMessage, WriteOutput, Publisher, HttpPipeline, JsonOptions);
            return true;
        }
    }
}
