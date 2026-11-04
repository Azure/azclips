// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// Tenant scope for <c>az deployment tenant create|validate|what-if</c>, <c>New-/Test-AzTenantDeployment</c> and
/// <c>Get-AzTenantDeploymentWhatIfResult</c>: owns Location, resolves the template and parameter inputs through the
/// scope-neutral <see cref="DeploymentRequestBuilder"/>, and runs preflight validation. Tenant deployments are always
/// Incremental, so there is no Mode input: the atomic processors send the model's default mode.
/// </summary>
public abstract class TenantDeploymentOrchestratorBase<TRequest, TResponse> : ResourcesProcessorBase<TRequest, TResponse>
    where TRequest : class
    where TResponse : class
{
    private readonly DeploymentRequestBuilder requestBuilder = new();

    public string Location { get; set; } = string.Empty;

    public string Name { get => requestBuilder.Name; set => requestBuilder.Name = value; }

    public string? TemplateFile { get => requestBuilder.TemplateFile; set => requestBuilder.TemplateFile = value; }

    public string? TemplateUri { get => requestBuilder.TemplateUri; set => requestBuilder.TemplateUri = value; }

    public string? TemplateSpecId { get => requestBuilder.TemplateSpecId; set => requestBuilder.TemplateSpecId = value; }

    public string? QueryString { get => requestBuilder.QueryString; set => requestBuilder.QueryString = value; }

    public string? ParametersFile { get => requestBuilder.ParametersFile; set => requestBuilder.ParametersFile = value; }

    public string? ParametersUri { get => requestBuilder.ParametersUri; set => requestBuilder.ParametersUri = value; }

    public string[]? Parameter { get => requestBuilder.Parameter; set => requestBuilder.Parameter = value; }

    public string? ValidationLevel { get => requestBuilder.ValidationLevel; set => requestBuilder.ValidationLevel = value; }

    // Resolve once per command so every request it sends shares one name (GUID when omitted) and one body,
    // and a Bicep source is compiled only once.
    protected ResolvedDeployment ResolveInputs()
    {
        requestBuilder.ValidateCommonInputs();
        object? template = requestBuilder.BuildTemplate(out TemplateLink? templateLink);
        object? parameters = requestBuilder.BuildParameters(out ParametersLink? parametersLink);
        return new ResolvedDeployment(template, templateLink, parameters, parametersLink, DeploymentRequestBuilder.NormalizeValidationLevel(ValidationLevel));
    }

    // POST validate; a template error (HTTP 200 or 400) throws like Test-AzResourceGroupDeployment.
    protected async Task<HttpResponseMessage> ValidateAsync(ResolvedDeployment resolved)
    {
        HttpResponseMessage response = await CreateExtendedProcessor<ValidateAtTenantScopeDeploymentsProcessor>(p =>
        {
            p.DeploymentName = Name;
            p.Location = Location;
            p.Template = resolved.Template;
            p.TemplateLink = resolved.TemplateLink;
            p.Parameters = resolved.Parameters;
            p.ParametersLink = resolved.ParametersLink;
            p.ValidationLevel = resolved.ValidationLevel;
        }).SendAsyncAllowBadRequest();

        string body = response.Content is null ? string.Empty : await response.Content.ReadAsStringAsync();
        if (!string.IsNullOrWhiteSpace(body) && JsonSerializer.Deserialize<DeploymentValidateResult>(body, JsonOptions)?.Error is { } error)
        {
            response.Dispose();
            throw new InvalidOperationException(TestResourceGroupDeploymentProcessor_Orchestrator.BuildErrorMessage(error));
        }

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

    protected sealed record ResolvedDeployment(
        object? Template,
        TemplateLink? TemplateLink,
        object? Parameters,
        ParametersLink? ParametersLink,
        string? ValidationLevel);
}
