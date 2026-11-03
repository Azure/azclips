// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    /// <summary>
    /// Exports the template used for a subscription-scoped deployment
    /// (POST /subscriptions/{sub}/providers/Microsoft.Resources/deployments/{name}/exportTemplate)
    /// and surfaces the template itself, matching <c>az deployment sub export</c>.
    /// </summary>
    public class ExportSubscriptionDeploymentTemplateProcessor : ResourcesProcessorBase<object, DeploymentExportResult>
    {
        private const string ApiVersion = "2025-04-01";

        public string SubscriptionId { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        protected override HttpMethod Method => HttpMethod.Post;

        protected override string CreateRequestUri() =>
            $"https://management.azure.com/subscriptions/{Uri.EscapeDataString(SubscriptionId)}/providers/Microsoft.Resources/deployments/{Uri.EscapeDataString(Name)}/exportTemplate?api-version={ApiVersion}";

        protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(DeploymentExportResult? body)
        {
            await WriteOutput(ExtractTemplate(body));
            return true;
        }

        protected JsonElement ExtractTemplate(DeploymentExportResult? body) =>
            body?.Template
            ?? throw new InvalidOperationException($"The export response for deployment '{Name}' did not contain a template.");
    }
}
