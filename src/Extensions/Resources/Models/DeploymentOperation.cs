// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.ExtensionRuntime.Json;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Models
{
    //
    // Summary:
    //     HTTP message captured for a deployment operation request or response.
    public class HttpMessage
    {
        // HTTP message content.
        public object? Content { get; set; }

        public override string ToString() => JsonNode.FromObject(this).ToString();
    }

    //
    // Summary:
    //     Operation status message object.
    public class StatusMessage
    {
        // Status of the deployment operation.
        public string? Status { get; set; }

        // The error reported by the operation.
        public ErrorResponse? Error { get; set; }

        public override string ToString() => JsonNode.FromObject(this).ToString();
    }

    //
    // Summary:
    //     Represents the value for an extension config property.
    public class DeploymentExtensionConfigItem
    {
        // The value of the extension config property.
        public object? Value { get; set; }

        // The Azure Key Vault reference used to retrieve the secret value of the property.
        public object? KeyVaultReference { get; set; }

        public override string ToString() => JsonNode.FromObject(this).ToString();
    }

    //
    // Summary:
    //     The extension a resource was deployed with.
    public class DeploymentExtensionDefinition
    {
        // The alias of the extension as defined in the deployment template.
        public string? Alias { get; set; }

        // The extension name.
        public string? Name { get; set; }

        // The extension version.
        public string? Version { get; set; }

        // The extension configuration ID. Uniquely identifies a deployment target within an extension.
        public string? ConfigId { get; set; }

        // The extension configuration hash.
        public string? ConfigHash { get; set; }

        // The extension configuration.
        public Dictionary<string, DeploymentExtensionConfigItem>? Config { get; set; }

        public override string ToString() => JsonNode.FromObject(this).ToString();
    }

    //
    // Summary:
    //     The resource targeted by a deployment operation.
    public class TargetResource
    {
        // The Azure resource ID of the resource.
        public string? Id { get; set; }

        // The name of the resource.
        public string? ResourceName { get; set; }

        // The type of the resource.
        public string? ResourceType { get; set; }

        // The extension the resource was deployed with.
        public DeploymentExtensionDefinition? Extension { get; set; }

        // The extensible resource identifiers.
        public object? Identifiers { get; set; }

        // The API version the resource was deployed with.
        public string? ApiVersion { get; set; }

        // The symbolic name of the resource as defined in the deployment template.
        public string? SymbolicName { get; set; }

        // The symbolic name path to the resource, including nested deployment(s) and extension if applicable.
        public List<string>? SymbolicNamePath { get; set; }

        public override string ToString() => JsonNode.FromObject(this).ToString();
    }

    //
    // Summary:
    //     Deployment operation properties.
    public class DeploymentOperationProperties
    {
        // The name of the current provisioning operation.
        public ProvisioningOperation? ProvisioningOperation { get; set; }

        // The state of the provisioning.
        public string? ProvisioningState { get; set; }

        // The date and time of the operation.
        public DateTime? Timestamp { get; set; }

        // The duration of the operation.
        public string? Duration { get; set; }

        // Deployment operation service request id.
        public string? ServiceRequestId { get; set; }

        // Operation status code from the resource provider. May be unset if no response has been received yet.
        public string? StatusCode { get; set; }

        // Operation status message from the resource provider. Only present when the provider reported an error.
        public StatusMessage? StatusMessage { get; set; }

        // The target resource.
        public TargetResource? TargetResource { get; set; }

        // The HTTP request message.
        public HttpMessage? Request { get; set; }

        // The HTTP response message.
        public HttpMessage? Response { get; set; }

        public override string ToString() => JsonNode.FromObject(this).ToString();
    }

    //
    // Summary:
    //     Deployment operation information.
    public class DeploymentOperation
    {
        // Full deployment operation ID.
        public string? Id { get; set; }

        // Deployment operation ID.
        public string? OperationId { get; set; }

        // Deployment operation properties.
        public DeploymentOperationProperties? Properties { get; set; }

        public override string ToString() => JsonNode.FromObject(this).ToString();
    }
}
