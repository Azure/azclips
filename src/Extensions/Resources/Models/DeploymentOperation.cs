// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.ExtensionRuntime.Json;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Models
{
    //
    // Summary:
    //     The HTTP request or response message captured for a deployment operation.
    public class HttpMessage
    {
        // HTTP message content.
        public object? Content { get; set; }

        public override string ToString() => JsonNode.FromObject(this).ToString();
    }

    //
    // Summary:
    //     Operation status message object. Only populated when the resource provider reported an error.
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
    //     Deployment operation properties.
    public class DeploymentOperationProperties
    {
        // The name of the current provisioning operation.
        public ProvisioningOperation? ProvisioningOperation { get; set; }

        // The state of the provisioning (e.g. Running, Succeeded, Failed).
        public string? ProvisioningState { get; set; }

        // The date and time of the operation.
        public DateTime? Timestamp { get; set; }

        // The duration of the operation.
        public string? Duration { get; set; }

        // Deployment operation service request id.
        public string? ServiceRequestId { get; set; }

        // Operation status code from the resource provider. Unset until a response has been received.
        public string? StatusCode { get; set; }

        // Operation status message from the resource provider. Only present when an error was received.
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
    //     Deployment operation information. One entry per resource action performed by a deployment.
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
    //     Result of the request to list deployment operations. ARM returns the standard
    //     { "value": [...], "nextLink": "..." } envelope.
    public class DeploymentOperationsListResult
    {
        // The deployment operations on this page.
        public List<DeploymentOperation>? Value { get; set; }

        // The URL to use for getting the next set of results.
        public string? NextLink { get; set; }

        public override string ToString() => JsonNode.FromObject(this).ToString();
    }
}
