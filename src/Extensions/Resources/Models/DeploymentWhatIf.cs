// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using Microsoft.Azure.Azclips.ExtensionRuntime.Json;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Models
{
    //
    // Summary:
    //     What-if settings for a deployment. Controls the verbosity of the predicted changes.
    public class DeploymentWhatIfSettings
    {
        // The result format of the what-if. ResourceIdOnly or FullResourcePayloads.
        public string ResultFormat { get; set; } = "FullResourcePayloads";

        public override string ToString() => JsonNode.FromObject(this).ToString();
    }

    //
    // Summary:
    //     Deployment what-if properties. Request properties for
    //     POST .../providers/Microsoft.Resources/deployments/{deploymentName}/whatIf.
    public class DeploymentWhatIfProperties
    {
        // The mode that is used to deploy resources. Incremental or Complete.
        public DeploymentMode Mode { get; set; } = DeploymentMode.Incremental;

        // The template content. Inline template syntax. Use either templateLink or template, but not both.
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object? Template { get; set; }

        // The URI of the template. Use either templateLink or template, but not both.
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public TemplateLink? TemplateLink { get; set; }

        // Inline deployment parameters. Use either parametersLink or parameters, but not both.
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object? Parameters { get; set; }

        // The URI of the parameters file. Use either parametersLink or parameters, but not both.
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ParametersLink? ParametersLink { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DebugSetting? DebugSetting { get; set; }

        // The validation level of the deployment. Template, Provider, or ProviderNoRbac.
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ValidationLevel { get; set; }

        // The what-if settings (result format).
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DeploymentWhatIfSettings? WhatIfSettings { get; set; }

        public override string ToString() => JsonNode.FromObject(this).ToString();
    }

    //
    // Summary:
    //     Deployment what-if operation parameters. Request body for
    //     POST .../providers/Microsoft.Resources/deployments/{deploymentName}/whatIf.
    public class DeploymentWhatIf
    {
        // The location to store the deployment data.
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Location { get; set; }

        // The deployment what-if properties.
        public DeploymentWhatIfProperties Properties { get; set; } = new();

        public override string ToString() => JsonNode.FromObject(this).ToString();
    }
}
