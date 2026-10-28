// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using System.Text.Json;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    /// <summary>
    /// Exports the template used for a resource-group-scoped deployment
    /// (POST .../resourcegroups/{rg}/providers/Microsoft.Resources/deployments/{name}/exportTemplate).
    /// The operation is read-only and synchronous: ARM answers 200 with
    /// <c>{ "template": { ... } }</c>. This processor unwraps that envelope and surfaces the
    /// template itself, matching <c>az deployment group export</c>.
    /// </summary>
    public class ExportResourceGroupDeploymentTemplateProcessor : ResourcesProcessorBase<object, DeploymentExportResult>
    {
        private const string ApiVersion = "2025-04-01";

        public string SubscriptionId { get; set; } = string.Empty;

        public string ResourceGroupName { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        protected override HttpMethod Method => HttpMethod.Post;

        protected override string CreateRequestUri() =>
            $"https://management.azure.com/subscriptions/{Uri.EscapeDataString(SubscriptionIdValidator.Require(SubscriptionId))}/resourcegroups/{Uri.EscapeDataString(ResourceGroupName)}/providers/Microsoft.Resources/deployments/{Uri.EscapeDataString(Name)}/exportTemplate?api-version={ApiVersion}";

        protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(DeploymentExportResult? body)
        {
            await WriteOutput(ExtractTemplate(body));
            return true;
        }

        /// <summary>
        /// Unwraps the ARM <c>{ "template": ... }</c> envelope. A success response without a
        /// template is not a usable export, so it is reported instead of yielding empty output.
        /// </summary>
        protected JsonElement ExtractTemplate(DeploymentExportResult? body) =>
            body?.Template
            ?? throw new InvalidOperationException($"The export response for deployment '{Name}' in resource group '{ResourceGroupName}' did not contain a template.");
    }
}
