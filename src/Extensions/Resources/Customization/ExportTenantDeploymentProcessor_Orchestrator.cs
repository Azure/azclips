// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// <c>az deployment tenant export</c>: writes only the exported <c>template</c> (legacy CLI behavior), and reports a
/// response without one like <see cref="ExportResourceGroupDeploymentTemplateProcessor"/>.
/// </summary>
public class ExportTenantDeploymentProcessor_Orchestrator : ExportTemplateAtTenantScopeDeploymentsProcessor
{
    protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(DeploymentExportResult? body)
    {
        await WriteOutput(ExtractTemplate(body));
        return true;
    }

    protected JsonElement ExtractTemplate(DeploymentExportResult? body) =>
        body?.Template
        ?? throw new InvalidOperationException($"The export response for deployment '{DeploymentName}' at tenant scope did not contain a template.");
}
