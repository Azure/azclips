// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary><c>az deployment tenant export</c>: writes only the exported <c>template</c> (legacy CLI behavior).</summary>
public class ExportTenantDeploymentProcessor_Orchestrator : ExportTemplateAtTenantScopeDeploymentsProcessor
{
    protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(DeploymentExportResult? body)
    {
        // Legacy prints json.dumps(result.template), so a missing template prints "null".
        await WriteOutput((object?)body?.Template ?? new SerializedJson("null"));
        return true;
    }

    /// <summary>Unwraps the ARM <c>{ "template": ... }</c> envelope for callers that need a template.</summary>
    protected JsonElement ExtractTemplate(DeploymentExportResult? body) =>
        body?.Template is JsonElement template
            ? template
            : throw new InvalidOperationException($"The export response for deployment '{DeploymentName}' at tenant scope did not contain a template.");
}
