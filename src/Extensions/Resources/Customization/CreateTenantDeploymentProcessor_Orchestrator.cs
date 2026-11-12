// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// <c>az deployment tenant create</c> / <c>New-AzTenantDeployment</c>: validate, then PUT and wait, like
/// <see cref="CreateManagementGroupDeploymentProcessor_Orchestrator"/>. CLI <c>--no-wait</c> and PowerShell <c>-NoWait</c>
/// stop after the PUT submission and write its initial response instead of waiting for the deployment to finish.
/// </summary>
public class CreateTenantDeploymentProcessor_Orchestrator : TenantDeploymentOrchestratorBase<ScopedDeployment, DeploymentExtended>
{
    public bool NoWait { get; set; }

    internal override async Task<HttpResponseMessage> SendAsync()
    {
        ResolvedDeployment resolved = ResolveInputs();
        using (await ValidateAsync(resolved))
        {
        }

        CreateOrUpdateAtTenantScopeDeploymentsProcessor put = CreateExtendedProcessor<CreateOrUpdateAtTenantScopeDeploymentsProcessor>(p =>
        {
            p.DeploymentName = Name;
            p.Body = BuildDeployment(resolved);
        });

        if (!NoWait)
        {
            return await put.SendAsync();
        }

        HttpResponseMessage response = await put.SendAsyncOnce();
        try
        {
            await response.EnsureSuccessAsync(Publisher);
        }
        catch
        {
            response.Dispose();
            throw;
        }

        return response;
    }
}
