// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// <c>Test-AzSubDeployment</c> / <c>az deployment sub validate</c>: the generated validate call plus
/// local template inputs. A local ARM JSON or Bicep file, a parameters or .bicepparam file, and
/// inline parameters are turned into the inline template and parameters before the request is sent.
/// </summary>
public class TestSubscriptionDeploymentProcessor_Orchestrator : ValidateAtSubscriptionScopeDeploymentsProcessor
{
    public string? TemplateFile { get; set; }

    public string? ParametersFile { get; set; }

    public string[]? Parameter { get; set; }

    protected override Task BeforeProcessAsync()
    {
        DeploymentRequestBuilder builder = new()
        {
            Name = DeploymentName,
            TemplateUri = TemplateLinkUri,
            TemplateFile = TemplateFile,
            TemplateSpecId = TemplateLinkId,
            TemplateObject = Template as Hashtable,
            QueryString = TemplateLinkQueryString,
            ParametersUri = ParametersLinkUri,
            ParametersFile = ParametersFile,
            Parameter = Parameter,
            Mode = Mode,
        };
        builder.ValidateCommonInputs();

        DeploymentName = builder.Name;
        Template = builder.BuildTemplate(out TemplateLink? templateLink);
        // The flattened link setters create an empty link object even for null, and the service
        // rejects a body with both a template and a templateLink, so set them only for a real link.
        if (templateLink is not null)
        {
            TemplateLinkUri = templateLink.Uri;
            TemplateLinkId = templateLink.Id;
            TemplateLinkQueryString = templateLink.QueryString;
        }
        Parameters = builder.BuildParameters(out ParametersLink? parametersLink);
        if (parametersLink is not null)
        {
            ParametersLinkUri = parametersLink.Uri;
        }
        return base.BeforeProcessAsync();
    }
}
