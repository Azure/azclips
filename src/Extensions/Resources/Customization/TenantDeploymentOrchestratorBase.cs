// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Text.Json;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// Tenant scope for <c>az deployment tenant create|validate|what-if</c> and <c>New-/Test-AzTenantDeployment</c>:
/// owns Location, builds the deployment properties, and runs preflight validation. The shared template, Bicep,
/// and parameter processing is delegated to the scope-neutral <see cref="DeploymentRequestBuilder"/>.
/// </summary>
public abstract class TenantDeploymentOrchestratorBase<TRequest, TResponse> : ProcessorBase<TRequest, TResponse>
    where TRequest : class
    where TResponse : class
{
    private readonly DeploymentRequestBuilder requestBuilder = new() { UpstreamTemplateInputs = true };

    public string Location { get; set; } = string.Empty;

    public string Name { get => requestBuilder.Name; set => requestBuilder.Name = value; }

    public string? TemplateUri { get => requestBuilder.TemplateUri; set => requestBuilder.TemplateUri = value; }

    public string? TemplateFile { get => requestBuilder.TemplateFile; set => requestBuilder.TemplateFile = value; }

    public string? TemplateSpecId { get => requestBuilder.TemplateSpecId; set => requestBuilder.TemplateSpecId = value; }

    public Hashtable? TemplateObject { get => requestBuilder.TemplateObject; set => requestBuilder.TemplateObject = value; }

    public string? QueryString { get => requestBuilder.QueryString; set => requestBuilder.QueryString = value; }

    public string? ParametersUri { get => requestBuilder.ParametersUri; set => requestBuilder.ParametersUri = value; }

    public string? ParametersFile { get => requestBuilder.ParametersFile; set => requestBuilder.ParametersFile = value; }

    public string[]? Parameter { get => requestBuilder.Parameter; set => requestBuilder.Parameter = value; }

    public string? ValidationLevel { get => requestBuilder.ValidationLevel; set => requestBuilder.ValidationLevel = value; }

    // Lets one tenant command run another, such as create running what-if, with the same template inputs.
    protected void CopyTemplateInputsTo<TOtherRequest, TOtherResponse>(TenantDeploymentOrchestratorBase<TOtherRequest, TOtherResponse> target)
        where TOtherRequest : class
        where TOtherResponse : class
    {
        target.Location = Location;
        target.Name = Name;
        target.TemplateUri = TemplateUri;
        target.TemplateFile = TemplateFile;
        target.TemplateSpecId = TemplateSpecId;
        target.TemplateObject = TemplateObject;
        target.QueryString = QueryString;
        target.ParametersUri = ParametersUri;
        target.ParametersFile = ParametersFile;
        target.Parameter = Parameter;
        target.ValidationLevel = ValidationLevel;
    }

    // Create, validate and what-if name an unnamed deployment after its template, like legacy CLI and PowerShell.
    protected DeploymentProperties BuildProperties()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            Name = DeriveName(TemplateFile, TemplateUri);
        }
        requestBuilder.ValidateCommonInputs();

        return new DeploymentProperties
        {
            // Like legacy tenant deployments, create, validate and what-if always use Incremental.
            Mode = nameof(DeploymentMode.Incremental),
            Template = requestBuilder.BuildTemplate(out TemplateLink? templateLink),
            TemplateLink = templateLink,
            Parameters = requestBuilder.BuildParameters(out ParametersLink? parametersLink),
            ParametersLink = parametersLink,
            ValidationLevel = DeploymentRequestBuilder.NormalizeValidationLevel(ValidationLevel),
        };
    }

    // Template file name, else the last segment of a template URI with a scheme, both without extension;
    // empty leaves ValidateCommonInputs to supply its GUID.
    internal static string DeriveName(string? templateFile, string? templateUri)
    {
        if (!string.IsNullOrWhiteSpace(templateFile))
        {
            return Path.GetFileNameWithoutExtension(templateFile);
        }
        return Uri.TryCreate(templateUri, UriKind.Absolute, out Uri? uri)
            ? Path.GetFileNameWithoutExtension(Uri.UnescapeDataString(uri.AbsolutePath))
            : string.Empty;
    }

    // POST validate; a template error throws like Test-AzResourceGroupDeployment.
    protected async Task<HttpResponseMessage> ValidateAsync(ScopedDeployment deployment)
    {
        HttpResponseMessage response = await CreateExtendedProcessor<ValidateAtTenantScopeDeploymentsProcessor>(p =>
        {
            p.DeploymentName = Name;
            p.Body = deployment;
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
