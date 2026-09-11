// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Customization;

public class NewManagementGroupDeploymentWhatIfProcessor
    : WhatIfAtManagementGroupScopeDeploymentsProcessor, IManagementGroupDeploymentInput
{
    public object? TemplateObject { get; set; }

    public object? TemplateParameterObject { get; set; }

    public string? ModeInput { get; set; }

    public string? TemplateFile { get; set; }

    public string? ParametersFile { get; set; }

    public string[]? Parameter { get; set; }

    public string? ResultFormatInput { get; set; }

    protected override Task BeforeProcessAsync()
    {
        ManagementGroupDeploymentPreparation.Prepare(this, JsonOptions);
        ResultFormat = ResultFormatInput?.Trim().ToLowerInvariant() switch
        {
            null or "" or "fullresourcepayloads" => "FullResourcePayloads",
            "resourceidonly" => "ResourceIdOnly",
            _ => throw new ArgumentException(
                "ResultFormat must be FullResourcePayloads or ResourceIdOnly."),
        };
        return base.BeforeProcessAsync();
    }

    protected override Task<bool> BeforeWriteOutputAsyncAndReturn(WhatIfOperationResult? result)
    {
        if (result is not null
            && (result.Error is not null
                || string.Equals(result.Status, "Failed", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                ManagementGroupDeploymentPreparation.BuildArmErrorMessage("What-if operation", result.Error));
        }

        return Task.FromResult(false);
    }
}
