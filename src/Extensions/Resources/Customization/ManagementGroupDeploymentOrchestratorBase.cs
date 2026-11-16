// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Text.Json;
using System.Text.Json.Serialization;
using Core.Abstractions;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// Management group scope for <c>az deployment mg create|validate|what-if</c> and <c>New-/Test-AzManagementGroupDeployment</c>:
/// owns ManagementGroupId and Location, builds the deployment properties, and runs preflight validation. The shared
/// template, Bicep, and parameter processing is delegated to the scope-neutral <see cref="DeploymentRequestBuilder"/>.
/// </summary>
public abstract class ManagementGroupDeploymentOrchestratorBase<TRequest, TResponse> : ResourcesProcessorBase<TRequest, TResponse>
    where TRequest : class
    where TResponse : class
{
    private readonly DeploymentRequestBuilder requestBuilder = new();

    // The generated deployment models carry fields ARM rejects even when null (e.g. externalInputDefinitions),
    // so omit nulls; child processors inherit these options through CreateExtendedProcessor.
    protected override void AfterInit()
    {
        base.AfterInit();
        JsonOptions = new JsonSerializerOptions(JsonOptions) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
    }

    public string ManagementGroupId { get; set; } = string.Empty;

    public string Location { get; set; } = string.Empty;

    public string Name { get => requestBuilder.Name; set => requestBuilder.Name = value; }

    public string? TemplateUri { get => requestBuilder.TemplateUri; set => requestBuilder.TemplateUri = value; }

    public string? TemplateFile { get => requestBuilder.TemplateFile; set => requestBuilder.TemplateFile = value; }

    public string? TemplateSpecId { get => requestBuilder.TemplateSpecId; set => requestBuilder.TemplateSpecId = value; }

    public Hashtable? TemplateObject { get => requestBuilder.TemplateObject; set => requestBuilder.TemplateObject = value; }

    public string? QueryString { get => requestBuilder.QueryString; set => requestBuilder.QueryString = value; }

    public string? ParametersUri { get => requestBuilder.ParametersUri; set => requestBuilder.ParametersUri = value; }

    public string? ParametersFile { get => requestBuilder.ParametersFile; set => requestBuilder.ParametersFile = value; }

    public string? Mode { get => requestBuilder.Mode; set => requestBuilder.Mode = value; }

    public string[]? Parameter { get => requestBuilder.Parameter; set => requestBuilder.Parameter = value; }

    public string? ValidationLevel { get => requestBuilder.ValidationLevel; set => requestBuilder.ValidationLevel = value; }

    protected DeploymentProperties BuildProperties()
    {
        if (string.IsNullOrWhiteSpace(ManagementGroupId))
        {
            throw AzclipsErrorException.InvalidInput("ManagementGroupId must be provided.");
        }
        if (string.IsNullOrWhiteSpace(Location))
        {
            throw AzclipsErrorException.InvalidInput("Location must be provided.");
        }
        requestBuilder.ValidateCommonInputs();

        return new DeploymentProperties
        {
            Mode = DeploymentRequestBuilder.ParseMode(Mode),
            Template = requestBuilder.BuildTemplate(out TemplateLink? templateLink),
            TemplateLink = templateLink,
            Parameters = requestBuilder.BuildParameters(out ParametersLink? parametersLink),
            ParametersLink = parametersLink,
            ValidationLevel = DeploymentRequestBuilder.NormalizeValidationLevel(ValidationLevel),
        };
    }

    // POST validate; a template error throws like Test-AzResourceGroupDeployment.
    protected async Task<HttpResponseMessage> ValidateAsync(ScopedDeployment deployment)
    {
        HttpResponseMessage response = await CreateExtendedProcessor<ValidateAtManagementGroupScopeDeploymentsProcessor>(p =>
        {
            p.GroupId = ManagementGroupId;
            p.DeploymentName = Name;
            // Keep the original body unchanged for the actual create request.
            p.Body = new ScopedDeployment
            {
                Location = deployment.Location,
                Tags = deployment.Tags,
                Properties = new DeploymentProperties
                {
                    Mode = nameof(DeploymentMode.Incremental),
                    Template = deployment.Properties.Template,
                    TemplateLink = deployment.Properties.TemplateLink,
                    Parameters = deployment.Properties.Parameters,
                    ParametersLink = deployment.Properties.ParametersLink,
                    OnErrorDeployment = deployment.Properties.OnErrorDeployment,
                    DebugSetting = deployment.Properties.DebugSetting,
                    ValidationLevel = deployment.Properties.ValidationLevel,
                },
            };
        }).SendAsyncAllowBadRequest();

        string body = response.Content is null ? string.Empty : await response.Content.ReadAsStringAsync();
        if (!string.IsNullOrWhiteSpace(body) && JsonSerializer.Deserialize<DeploymentValidateResult>(body, JsonOptions)?.Error is { } error)
        {
            response.Dispose();
            throw new InvalidOperationException(TestResourceGroupDeploymentProcessor_Orchestrator.BuildErrorMessage(error));
        }
        if (!response.IsSuccessStatusCode)
        {
            await response.EnsureSuccessAsync(Publisher);
        }
        return response;
    }
}
