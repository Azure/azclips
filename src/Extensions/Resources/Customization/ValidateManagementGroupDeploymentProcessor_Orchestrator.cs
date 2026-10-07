// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

public class ValidateManagementGroupDeploymentProcessor_Orchestrator
    : ManagementGroupDeploymentOrchestratorBase<ScopedDeployment, DeploymentValidateResult>
{
    public bool ThrowOnError { get; set; } = true;

    public bool SuppressSuccessOutput { get; set; }

    internal override async Task<HttpResponseMessage> SendAsync()
    {
        PrepareAndValidate();
        ValidateAtManagementGroupScopeDeploymentsProcessor processor =
            CreateExtendedProcessor<ValidateAtManagementGroupScopeDeploymentsProcessor>(p =>
            {
                p.GroupId = ManagementGroupId;
                p.DeploymentName = Name;
                p.Body = BuildDeployment(forceIncrementalMode: true);
            });

        if (ThrowOnError)
        {
            return await processor.SendAsync();
        }

        return await processor.SendWithoutStatusValidationAsync();
    }

    protected override Task<bool> BeforeWriteOutputAsyncAndReturn(DeploymentValidateResult? body)
    {
        if (ThrowOnError)
        {
            ThrowIfError(body?.Error, "Management group deployment validation");
        }
        return Task.FromResult(SuppressSuccessOutput && body?.Error is null);
    }
}

public partial class ValidateAtManagementGroupScopeDeploymentsProcessor
{
    internal async Task<HttpResponseMessage> SendWithoutStatusValidationAsync()
    {
        using HttpRequestMessage request = await CreateRequestMessage();
        HttpResponseMessage response = await HttpPipeline.SendAsync(request, Publisher);
        await Publisher.Publish(Events.TracingHttpResponseMessage, response);
        return response;
    }
}
