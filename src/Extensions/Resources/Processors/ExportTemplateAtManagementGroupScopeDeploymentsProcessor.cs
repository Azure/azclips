// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

public class ExportTemplateAtManagementGroupScopeDeploymentsProcessor
    : ResourcesProcessorBase<object, DeploymentExportResult>
{
    private const string ApiVersion = "2025-04-01";

    public string ManagementGroupId { get; set; } = string.Empty;

    public string DeploymentName { get; set; } = string.Empty;

    protected override HttpMethod Method => HttpMethod.Post;

    protected override string CreateRequestUri() =>
        $"{ManagementGroupDeploymentUri.Resource(ManagementGroupId, DeploymentName)}/exportTemplate?api-version={ApiVersion}";

    protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(DeploymentExportResult? body)
    {
        if (body?.Template is not null)
        {
            await WriteOutput(body.Template);
        }
        return true;
    }
}
