// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// <c>az deployment tenant validate</c> / <c>Test-AzTenantDeployment</c>: POST validate, throw on a template error,
/// otherwise write the result, like <see cref="TestResourceGroupDeploymentProcessor_Orchestrator"/>.
/// With <see cref="PowerShellOutput"/> it follows legacy Test-AzTenantDeployment instead.
/// </summary>
public class ValidateTenantDeploymentProcessor_Orchestrator : TenantDeploymentOrchestratorBase<ScopedDeployment, DeploymentValidateResult>
{
    // Set only by Test-AzTenantDeployment: write any validate failure as the error object instead of throwing, and nothing on success.
    public bool PowerShellOutput { get; set; }

    public bool SuppressDiagnostics { get; set; }

    internal override Task<HttpResponseMessage> SendAsync() =>
        ValidateAsync(new ScopedDeployment { Location = Location, Properties = BuildProperties() }, throwOnError: !PowerShellOutput);

    protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(DeploymentValidateResult? body)
    {
        if (!PowerShellOutput)
        {
            return false;
        }
        if (body?.Error is { } error)
        {
            await WriteOutput(error);
        }
        else if (!SuppressDiagnostics && WhatIfReportFormatter.FormatDiagnostics(body?.Properties?.Diagnostics) is { Length: > 0 } diagnostics)
        {
            await Publisher.Publish(Events.Warning, new EventData { Message = diagnostics });
        }
        return true;
    }
}
