// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

public class ExportTemplateAtTenantScopeDeploymentsProcessor : ResourcesProcessorBase<object, DeploymentExportResult>
{
    private const string ApiVersion = "2025-04-01";

    public string DeploymentName { get; set; } = string.Empty;

    protected override HttpMethod Method => HttpMethod.Post;

    protected override string CreateRequestUri() =>
        $"https://management.azure.com/providers/Microsoft.Resources/deployments/{Uri.EscapeDataString(DeploymentName)}/exportTemplate?api-version={ApiVersion}";

    protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(DeploymentExportResult? body)
    {
        if (body?.Template is not null)
        {
            await WriteOutput(body.Template);
        }
        return true;
    }
}
