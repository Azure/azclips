// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Deployment.Models;
using Microsoft.Azure.Azclips.Extensions.Deployment.Processors;

namespace Microsoft.Azure.Azclips.Extensions.Deployment.Customization;

public class TestManagementGroupDeploymentProcessor
    : ValidateAtManagementGroupScopeDeploymentsProcessor, IManagementGroupDeploymentInput
{
    public object? TemplateObject { get; set; }

    public object? TemplateParameterObject { get; set; }

    public string? ModeInput { get; set; }

    public string? TemplateFile { get; set; }

    public string? ParametersFile { get; set; }

    public string[]? Parameter { get; set; }

    protected override Task BeforeProcessAsync()
    {
        ManagementGroupDeploymentPreparation.Prepare(this, JsonOptions);
        return base.BeforeProcessAsync();
    }

    protected override Task<bool> BeforeWriteOutputAsyncAndReturn(DeploymentValidateResult? result)
    {
        if (result?.Error is not null)
        {
            throw new InvalidOperationException(
                ManagementGroupDeploymentPreparation.BuildArmErrorMessage("Deployment validation", result.Error));
        }

        return Task.FromResult(false);
    }
}
