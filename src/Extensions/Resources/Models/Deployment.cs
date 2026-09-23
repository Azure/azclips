// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using Microsoft.Azure.Azclips.ExtensionRuntime.Json;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Models
{
    //
    // Summary:
    //     Deployment properties.
    public class DeploymentProperties
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

        // The deployment on error behavior (rollback-on-error).
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public OnErrorDeployment? OnErrorDeployment { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DebugSetting? DebugSetting { get; set; }

        // The validation level of the deployment. Template, Provider, or ProviderNoRbac.
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ValidationLevel { get; set; }

        public override string ToString() => JsonNode.FromObject(this).ToString();
    }

    public class DebugSetting
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? DetailLevel { get; set; }

        public override string ToString() => JsonNode.FromObject(this).ToString();
    }

    //
    // Summary:
    //     Deployment operation parameters. Request body for
    //     PUT .../providers/Microsoft.Resources/deployments/{deploymentName}.
    public class Deployment
    {
        // The location to store the deployment data.
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Location { get; set; }

        // The deployment properties.
        public DeploymentProperties Properties { get; set; } = new();

        // Deployment tags.
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IDictionary<string, string>? Tags { get; set; }

        public override string ToString() => JsonNode.FromObject(this).ToString();
    }
}
