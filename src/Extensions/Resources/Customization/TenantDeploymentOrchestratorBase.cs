// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Net;
using System.Text.Json;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// Tenant scope for <c>az deployment tenant create|validate|what-if</c> and <c>New-/Test-AzTenantDeployment</c>:
/// owns Location, builds the deployment properties, and runs preflight validation. The shared template, Bicep,
/// and parameter processing is delegated to the scope-neutral <see cref="DeploymentRequestBuilder"/>.
/// </summary>
public abstract class TenantDeploymentOrchestratorBase<TRequest, TResponse> : ResourcesProcessorBase<TRequest, TResponse>
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
            Mode = DeploymentMode.Incremental,
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

    // POST validate; a template error throws like Test-AzResourceGroupDeployment, or with throwOnError false
    // is returned in the response body.
    protected async Task<HttpResponseMessage> ValidateAsync(ScopedDeployment deployment, bool throwOnError = true)
    {
        Action<ValidateAtTenantScopeDeploymentsProcessor> configure = p =>
        {
            p.DeploymentName = Name;
            p.Body = deployment;
        };
        HttpResponseMessage response = throwOnError
            ? await CreateExtendedProcessor<ValidateAtTenantScopeLocationProcessor>(configure).SendAsyncAllowBadRequest()
            : await CreateExtendedProcessor<ValidateAtTenantScopeKeepingErrorsProcessor>(configure).SendKeepingErrorsAsync();

        string body = response.Content is null ? string.Empty : await response.Content.ReadAsStringAsync();
        if (!throwOnError && !response.IsSuccessStatusCode && !HasArmError(body))
        {
            // Like legacy Test-AzTenantDeployment, report any failed request (not only a 400) as the error object.
            body = JsonSerializer.Serialize(new { error = new { code = response.StatusCode.ToString(), message = $"{(int)response.StatusCode} {response.ReasonPhrase}: {body}" } });
        }
        if (!string.IsNullOrWhiteSpace(body) && JsonSerializer.Deserialize<DeploymentValidateResult>(body, JsonOptions)?.Error is { } error)
        {
            if (!throwOnError)
            {
                response.Content = new StringContent(body);
                return response;
            }
            response.Dispose();
            throw new InvalidOperationException(TestResourceGroupDeploymentProcessor_Orchestrator.BuildErrorMessage(error));
        }
        if (!response.IsSuccessStatusCode)
        {
            await response.EnsureSuccessAsync(Publisher);
        }
        return response;
    }

    private bool HasArmError(string body)
    {
        try
        {
            return JsonSerializer.Deserialize<DeploymentValidateResult>(body, JsonOptions)?.Error is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

/// <summary>
/// Tenant validate polled only via Location, like the resource-group validate: Azure-AsyncOperation and
/// Operation-Location return just a status without the validation result, and the poller would prefer them.
/// </summary>
internal class ValidateAtTenantScopeLocationProcessor : ValidateAtTenantScopeDeploymentsProcessor
{
    protected override Func<HttpResponseMessage, string?> GetOperationLocationUri => _ => null;
    protected override Func<HttpResponseMessage, string?> GetAsyncOperationUri => _ => null;
}

/// <summary>
/// Tenant validate for legacy Test-AzTenantDeployment: returns every failed status untouched, so no failure is
/// published to the error stream and the caller can write it as the error object. A 202 is polled via Location.
/// </summary>
internal sealed class ValidateAtTenantScopeKeepingErrorsProcessor : ValidateAtTenantScopeLocationProcessor
{
    internal async Task<HttpResponseMessage> SendKeepingErrorsAsync()
    {
        HttpResponseMessage response;
        using (HttpRequestMessage request = await CreateRequestMessage())
        {
            response = await HttpPipeline.SendAsync(request, Publisher);
        }
        await Publisher.Publish(Events.TracingHttpResponseMessage, response);

        if (response.StatusCode != HttpStatusCode.Accepted || GetLocationUri(response) is not { } location)
        {
            return response;
        }
        response.Dispose();
        return await HttpRuntime.SendAsyncLRO(
            () => Task.FromResult(new HttpRequestMessage(HttpMethod.Get, location)),
            Publisher, HttpPipeline, GetFinalUri, GetOperationLocationUri, GetAsyncOperationUri, GetLocationUri);
    }
}
