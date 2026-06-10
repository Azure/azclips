using Microsoft.Azure.Cirrus.ExtensionRuntime.Json;

namespace Microsoft.Azure.Cirrus.Extensions.Resources.Models
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

        // The hash produced for the template.
        public string? TemplateHash { get; set; }

        // The deployment mode. Incremental or Complete.
        public DeploymentMode? Mode { get; set; }

        // Array of provisioned resources.
        public List<ResourceReference>? OutputResources { get; set; }

        // The deployment error, if any.
        public object? Error { get; set; }

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
