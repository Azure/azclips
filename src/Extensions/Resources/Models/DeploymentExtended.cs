// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.ExtensionRuntime.Json;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Models
{
    //
    // Summary:
    //     Deployment properties with additional details. Returned by the deployment API.
    public class DeploymentPropertiesExtended
    {
        // Denotes the state of provisioning (e.g. Running, Succeeded, Failed, Canceled).
        public string? ProvisioningState { get; set; }

        // The correlation ID of the deployment.
        public string? CorrelationId { get; set; }

        // The timestamp of the template deployment.
        public DateTime? Timestamp { get; set; }

        // The duration of the template deployment.
        public string? Duration { get; set; }

        // Key/value pairs that represent deployment output.
        public object? Outputs { get; set; }

        // The list of resource providers needed for the deployment.
        public object? Providers { get; set; }

        // The list of deployment dependencies.
        public object? Dependencies { get; set; }

        // The URI referencing the template.
        public TemplateLink? TemplateLink { get; set; }

        // Deployment parameters.
        public object? Parameters { get; set; }

        // The URI referencing the parameters.
        public ParametersLink? ParametersLink { get; set; }

        // The extensions used in this deployment.
        public object? Extensions { get; set; }

        // The deployment mode. Incremental or Complete.
        public DeploymentMode? Mode { get; set; }

        // The debug setting of the deployment.
        public DebugSetting? DebugSetting { get; set; }

        // The deployment on error behavior.
        public OnErrorDeploymentExtended? OnErrorDeployment { get; set; }

        // The hash produced for the template.
        public string? TemplateHash { get; set; }

        // Array of provisioned resources.
        public List<ResourceReference>? OutputResources { get; set; }

        // Array of validated resources.
        public List<ResourceReference>? ValidatedResources { get; set; }

        // The deployment error, if any.
        public object? Error { get; set; }

        // Contains diagnostic information collected during validation process.
        public object? Diagnostics { get; set; }

        // The validation level of the deployment.
        public string? ValidationLevel { get; set; }

        public override string ToString() => JsonNode.FromObject(this).ToString();
    }

    //
    // Summary:
    //     Deployment on error behavior with additional details.
    public class OnErrorDeploymentExtended
    {
        // The state of the provisioning for the on error deployment.
        public string? ProvisioningState { get; set; }

        // The deployment on error behavior type. LastSuccessful or SpecificDeployment.
        public string? Type { get; set; }

        // The deployment to be used on error case.
        public string? DeploymentName { get; set; }

        public override string ToString() => JsonNode.FromObject(this).ToString();
    }

    //
    // Summary:
    //     The resource Id model. Identifies a resource provisioned by the deployment.
    public class ResourceReference
    {
        // The fully qualified Azure resource ID.
        public string? Id { get; set; }

        // The resource type.
        public string? ResourceType { get; set; }

        // The API version the resource was deployed with.
        public string? ApiVersion { get; set; }

        // The extensible resource identifiers.
        public object? Identifiers { get; set; }

        public override string ToString() => JsonNode.FromObject(this).ToString();
    }

    //
    // Summary:
    //     Deployment information. Response body for the deployment API.
    public class DeploymentExtended
    {
        // The ID of the deployment.
        public string? Id { get; set; }

        // The name of the deployment.
        public string? Name { get; set; }

        // The type of the deployment.
        public string? Type { get; set; }

        // The location of the deployment.
        public string? Location { get; set; }

        // Deployment properties.
        public DeploymentPropertiesExtended? Properties { get; set; }

        // Deployment tags.
        public IDictionary<string, string>? Tags { get; set; }

        public override string ToString() => JsonNode.FromObject(this).ToString();
    }
}
