// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// <c>Test-AzManagementGroupDeployment</c>: like legacy PS, writes validation errors as output instead of
/// throwing, outputs nothing on success, and shows diagnostics as a warning unless suppressed.
/// </summary>
public class TestManagementGroupDeploymentProcessor_Orchestrator : ValidateManagementGroupDeploymentProcessor_Orchestrator
{
    public bool SuppressDiagnostics { get; set; }

    protected override bool PromptsForMissingParameters => false;

    // Legacy PS rule: template URI/file basename without extension, else a new GUID.
    protected override string DefaultDeploymentName()
    {
        string? source = string.IsNullOrWhiteSpace(TemplateUri) ? TemplateFile : TemplateUri;
        return string.IsNullOrWhiteSpace(source) ? Guid.NewGuid().ToString() : Path.GetFileNameWithoutExtension(source);
    }

    protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(DeploymentValidateResult? body)
    {
        if (body?.Error is { } error)
        {
            await WriteOutput(new List<ErrorResponse> { error });
            return true;
        }
        Verbose("Template is valid.");
        if (!SuppressDiagnostics && body?.Properties?.Diagnostics is { Count: > 0 } diagnostics)
        {
            Warn($"\n\nDiagnostics ({diagnostics.Count}): \n" + string.Concat(diagnostics.Select(d => $"({d.Target}) {d.Message} ({d.Code})\n")));
        }
        return true;
    }

}
