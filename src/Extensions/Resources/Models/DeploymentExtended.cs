// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Azure.Azclips.ExtensionRuntime.Json;
using StjConverter = System.Text.Json.Serialization.JsonConverter<object?>;
using StjConverterAttribute = System.Text.Json.Serialization.JsonConverterAttribute;

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
        public Dictionary<string, DeploymentVariable>? Outputs { get; set; }

        // The hash produced for the template.
        public string? TemplateHash { get; set; }

        // The deployment mode. Incremental or Complete.
        public DeploymentMode? Mode { get; set; }

        // Array of provisioned resources.
        public List<ResourceReference>? OutputResources { get; set; }

        // The deployment error, if any.
        public object? Error { get; set; }

        // Deployment parameters.
        public Dictionary<string, DeploymentVariable>? Parameters { get; set; }

        // The URI referencing the template.
        public TemplateLink? TemplateLink { get; set; }

        // The URI referencing the parameters.
        public ParametersLink? ParametersLink { get; set; }

        // The deployment on error behavior.
        public OnErrorDeploymentExtended? OnErrorDeployment { get; set; }

        // The list of resource providers needed for the deployment.
        public object? Providers { get; set; }

        // The list of deployment dependencies.
        public object? Dependencies { get; set; }

        // The extensions used in this deployment.
        public object? Extensions { get; set; }

        // The debug setting of the deployment.
        public DebugSetting? DebugSetting { get; set; }

        // Array of validated resources.
        public List<ResourceReference>? ValidatedResources { get; set; }

        // Contains diagnostic information collected during validation process.
        public object? Diagnostics { get; set; }

        // The validation level of the deployment.
        public string? ValidationLevel { get; set; }

        public override string ToString() => JsonNode.FromObject(this).ToString();
    }

    //
    // Summary:
    //     Deployment on error behavior with additional details.
    public class OnErrorDeploymentExtended : OnErrorDeployment
    {
        // The state of the provisioning for the on error deployment.
        public string? ProvisioningState { get; set; }
    }

    //
    // Summary:
    //     A deployment parameter or output entry. Mirrors legacy Az.Resources DeploymentVariable
    //     so scripts can use $deployment.Outputs["name"].Value.
    public class DeploymentVariable
    {
        public string? Type { get; set; }

        // Unwrapped to plain values (string, long, double, bool, Dictionary, List) instead of JsonElement.
        [StjConverterAttribute(typeof(PlainValueConverter))]
        public object? Value { get; set; }

        private sealed class PlainValueConverter : StjConverter
        {
            public override bool HandleNull => true;

            public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                using var document = JsonDocument.ParseValue(ref reader);
                return ToPlain(document.RootElement);
            }

            public override void Write(Utf8JsonWriter writer, object? value, JsonSerializerOptions options)
                => System.Text.Json.JsonSerializer.Serialize(writer, value, value?.GetType() ?? typeof(object), options);

            private static object? ToPlain(JsonElement element) => element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.TryGetInt64(out long l) ? (object)l : element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => ToPlain(p.Value)),
                JsonValueKind.Array => element.EnumerateArray().Select(ToPlain).ToList(),
                _ => null,
            };
        }
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

        // Legacy Az.Resources PSResourceGroupDeployment top-level properties, kept for script
        // compatibility. Hidden from JSON so CLI output keeps the ARM shape.
        [JsonIgnore, IgnoreDataMember]
        public string? DeploymentName => Name;

        [JsonIgnore, IgnoreDataMember]
        public string? ResourceGroupName
        {
            get
            {
                string[] segments = Id?.Split('/', StringSplitOptions.RemoveEmptyEntries) ?? [];
                int index = Array.FindIndex(segments, s => s.Equals("resourceGroups", StringComparison.OrdinalIgnoreCase));
                return index >= 0 && index + 1 < segments.Length ? segments[index + 1] : null;
            }
        }

        [JsonIgnore, IgnoreDataMember]
        public string? ProvisioningState => Properties?.ProvisioningState;

        [JsonIgnore, IgnoreDataMember]
        public string? CorrelationId => Properties?.CorrelationId;

        [JsonIgnore, IgnoreDataMember]
        public DateTime? Timestamp => Properties?.Timestamp;

        [JsonIgnore, IgnoreDataMember]
        public DeploymentMode? Mode => Properties?.Mode;

        [JsonIgnore, IgnoreDataMember]
        public Dictionary<string, DeploymentVariable>? Parameters => Properties?.Parameters;

        [JsonIgnore, IgnoreDataMember]
        public Dictionary<string, DeploymentVariable>? Outputs => Properties?.Outputs;

        [JsonIgnore, IgnoreDataMember]
        public TemplateLink? TemplateLink => Properties?.TemplateLink;

        [JsonIgnore, IgnoreDataMember]
        public OnErrorDeployment? OnErrorDeployment => Properties?.OnErrorDeployment;

        public override string ToString() => JsonNode.FromObject(this).ToString();
    }
}
