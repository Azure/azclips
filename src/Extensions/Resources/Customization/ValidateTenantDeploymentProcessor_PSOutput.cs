// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// PowerShell-only <see cref="ValidateTenantDeploymentProcessor_Orchestrator"/> that follows legacy Test-AzTenantDeployment:
/// a validate failure is written as <see cref="PSResourceManagerError"/> objects instead of throwing, and a valid
/// deployment writes nothing (diagnostics go to the warning stream unless <see cref="SuppressDiagnostics"/>).
/// Only the PowerShell command table routes here, so the CLI keeps throwing and printing the validate result.
/// </summary>
public class ValidateTenantDeploymentProcessor_PSOutput : ValidateTenantDeploymentProcessor_Orchestrator
{
    public bool SuppressDiagnostics { get; set; }

    internal override Task<HttpResponseMessage> SendAsync() => ValidateAsync(BuildDeployment(), throwOnError: false);

    protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(DeploymentValidateResult? body)
    {
        if (body?.Error is { } error)
        {
            await WriteOutput(new List<PSResourceManagerError> { PSResourceManagerError.From(error) });
        }
        else if (!SuppressDiagnostics && WhatIfReportFormatter.FormatDiagnostics(body?.Properties?.Diagnostics) is { Length: > 0 } diagnostics)
        {
            await Publisher.Publish(Events.Warning, new EventData { Message = diagnostics });
        }
        return true;
    }
}