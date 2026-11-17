// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary><c>az deployment mg export</c>: writes only the exported <c>template</c> (legacy CLI behavior).</summary>
public class ExportManagementGroupDeploymentProcessor_Orchestrator : ExportTemplateAtManagementGroupScopeDeploymentsProcessor
{
    protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(DeploymentExportResult? body)
    {
        // Legacy prints json.dumps(result.template), so a missing template prints "null".
        await WriteOutput((object?)body?.Template ?? new SerializedJson("null"));
        return true;
    }
}
