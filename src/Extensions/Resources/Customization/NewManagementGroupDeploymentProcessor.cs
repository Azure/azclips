// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Text.Json;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

public class NewManagementGroupDeploymentProcessor
    : CreateOrUpdateAtManagementGroupScopeDeploymentsProcessor, IManagementGroupDeploymentInput
{
    public object? TemplateObject { get; set; }

    public object? TemplateParameterObject { get; set; }

    public object? Tag { get; set; }

    public string? ModeInput { get; set; }

    public string? TemplateFile { get; set; }

    public string? ParametersFile { get; set; }

    public string[]? Parameter { get; set; }

    public bool NoWait { get; set; }

    protected override Task BeforeProcessAsync()
    {
        ManagementGroupDeploymentPreparation.Prepare(this, JsonOptions);

        if (Tag is IDictionary tagObject)
        {
            Tags = tagObject.Keys.Cast<object>().ToDictionary(
                key => key.ToString()!,
                key => tagObject[key]?.ToString() ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);
        }

        return base.BeforeProcessAsync();
    }

    internal override async Task<HttpResponseMessage> SendAsync()
    {
        ValidateAtManagementGroupScopeDeploymentsProcessor validate =
            CreateExtendedProcessor<ValidateAtManagementGroupScopeDeploymentsProcessor>(processor =>
            {
                processor.ManagementGroupId = ManagementGroupId;
                processor.Name = Name;
                processor.Location = Location;
                processor.Template = Template;
                processor.TemplateUri = TemplateUri;
                processor.TemplateSpecId = TemplateSpecId;
                processor.TemplateRelativePath = TemplateRelativePath;
                processor.TemplateContentVersion = TemplateContentVersion;
                processor.QueryString = QueryString;
                processor.Parameters = Parameters;
                processor.ParametersUri = ParametersUri;
                processor.ParametersContentVersion = ParametersContentVersion;
                processor.Mode = Mode;
                processor.ValidationLevel = ValidationLevel;
                processor.DeploymentDebugLogLevel = DeploymentDebugLogLevel;
                processor.Tags = Tags;
            });

        using HttpResponseMessage validationResponse = await validate.SendAsync();
        await ThrowIfValidationFailedAsync(validationResponse);

        return NoWait
            ? await HttpRuntime.SendAsync(CreateRequestMessage, Publisher, HttpPipeline)
            : await base.SendAsync();
    }

    private async Task ThrowIfValidationFailedAsync(HttpResponseMessage response)
    {
        if (response.Content is null)
        {
            throw new InvalidOperationException("Deployment validation returned no response content.");
        }

        string rawBody = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(rawBody))
        {
            throw new InvalidOperationException("Deployment validation returned an empty response body.");
        }

        DeploymentValidateResult result;
        try
        {
            result = JsonSerializer.Deserialize<DeploymentValidateResult>(rawBody, JsonOptions)
                ?? throw new InvalidOperationException("Deployment validation returned no result.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Failed to deserialize deployment validation response. Raw body: {rawBody}", ex);
        }

        if (result.Error is not null)
        {
            throw new InvalidOperationException(
                ManagementGroupDeploymentPreparation.BuildArmErrorMessage("Deployment validation", result.Error));
        }
    }
}
