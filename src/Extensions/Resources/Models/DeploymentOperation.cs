// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.ExtensionRuntime.Json;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Models
{
    //
    // Summary:
    //     Deployment operation information. A single provisioning step recorded by ARM while
    //     it executes a deployment.
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

        // Operation status code from the resource provider. May be unset when no response has
        // been received yet.
        public string? StatusCode { get; set; }

        // Operation status message from the resource provider. Only provided when the resource
        // provider returned an error.
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
    //     HTTP message captured for a deployment operation.
    public class HttpMessage
    {
        // HTTP message content.
        public object? Content { get; set; }

        public override string ToString() => JsonNode.FromObject(this).ToString();
    }
}
