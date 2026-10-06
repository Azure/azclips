// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    /// <summary>
    /// Atomic processor: lists tenant-scoped deployments, draining every page into one flat array.
    /// </summary>
    public class ListTenantDeploymentProcessor : ResourcesProcessorBase<object, DeploymentListResult>
    {
        private const string ApiVersion = "2026-06-01";

        /// <summary>OData filter expression, e.g. <c>provisioningState eq 'Succeeded'</c>.</summary>
        public string? Filter { get; set; }

        protected override HttpMethod Method => HttpMethod.Get;

        protected override string CreateRequestUri()
        {
            string uri = $"https://management.azure.com/providers/Microsoft.Resources/deployments?api-version={ApiVersion}";
            return string.IsNullOrWhiteSpace(Filter) ? uri : $"{uri}&$filter={Uri.EscapeDataString(Filter)}";
        }

        private string? nextLink(DeploymentListResult body) => body.NextLink;
        private List<DeploymentExtended>? pageItems(DeploymentListResult body) => body.Value;

        protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(DeploymentListResult? body)
        {
            await body.WriteOutputWithPaginationAsync(pageItems, nextLink, CreateRequestMessage, WriteOutput, Publisher, HttpPipeline, JsonOptions);
            return true;
        }
    }
}
