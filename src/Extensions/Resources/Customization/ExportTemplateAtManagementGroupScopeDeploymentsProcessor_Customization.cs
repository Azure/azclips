// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

public class ExportTemplateAtManagementGroupScopeDeploymentsProcessor_Customization
    : ExportTemplateAtManagementGroupScopeDeploymentsProcessor
{
    protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(DeploymentExportResult? body)
    {
        if (body?.Template is not null)
        {
            await WriteOutput(body.Template);
        }
        return true;
    }
}
