using Microsoft.Azure.Cirrus.ExtensionRuntime.Json;

namespace Microsoft.Azure.Cirrus.Extensions.Resources.Models
{
    //
    // Summary:
    //     The resource management error response. Mirrors ARM's ErrorResponse shape.
    public class ErrorResponse
    {
        // The error code.
        public string? Code { get; set; }

        // The error message.
        public string? Message { get; set; }

        // The error target.
        public string? Target { get; set; }

        // The error details.
        public List<ErrorResponse>? Details { get; set; }

        // The error additional info.
        public object? AdditionalInfo { get; set; }

        public override string ToString() => JsonNode.FromObject(this).ToString();
    }

    //
    // Summary:
    //     The predicted change to an individual property of a resource.
    public class WhatIfPropertyChange
    {
        // The path of the property.
        public string? Path { get; set; }

        // The type of property change. Create, Delete, Modify, Array, or NoEffect.
        public string? PropertyChangeType { get; set; }

        // The value of the property before the deployment is executed.
        public object? Before { get; set; }

        // The value of the property after the deployment is executed.
        public object? After { get; set; }

        // Nested property changes.
        public List<WhatIfPropertyChange>? Children { get; set; }

        public override string ToString() => JsonNode.FromObject(this).ToString();
    }

    //
    // Summary:
    //     Information about a single predicted resource change in a what-if operation.
    public class WhatIfChange
    {
        // The resource id.
        public string? ResourceId { get; set; }

        // The type of change. Create, Delete, Ignore, Deploy, NoChange, Modify, or Unsupported.
        public string? ChangeType { get; set; }

        // The snapshot of the resource before the deployment is executed.
        public object? Before { get; set; }

        // The predicted snapshot of the resource after the deployment is executed.
        public object? After { get; set; }

        // The predicted changes to resource properties.
        public List<WhatIfPropertyChange>? Delta { get; set; }

        // The resource id of the deployment responsible for the change.
        public string? DeploymentId { get; set; }

        // The symbolic name of the resource responsible for the change.
        public string? SymbolicName { get; set; }

        // The explanation about why the resource is unsupported by what-if.
        public string? UnsupportedReason { get; set; }

        public override string ToString() => JsonNode.FromObject(this).ToString();
    }

    //
    // Summary:
    //     Deployment what-if properties. Contains the predicted change list.
    public class WhatIfOperationProperties
    {
        // List of resource changes predicted by the what-if operation.
        public List<WhatIfChange>? Changes { get; set; }

        // List of resource changes predicted by deployment stacks (potential changes).
        public List<WhatIfChange>? PotentialChanges { get; set; }

        // List of resource diagnostics detected by the what-if operation.
        public object? Diagnostics { get; set; }

        public override string ToString() => JsonNode.FromObject(this).ToString();
    }

    //
    // Summary:
    //     Result of a deployment what-if operation. Returned by the what-if API.
    //     Note: the operation may return HTTP 200 with status Succeeded even when the template
    //     has an error; the error is reported in the Error property, not via HTTP status.
    public class WhatIfOperationResult
    {
        // Status of the what-if operation (e.g. Succeeded, Failed).
        public string? Status { get; set; }

        // Error when the what-if operation fails (surfaced even on HTTP 200).
        public ErrorResponse? Error { get; set; }

        // The what-if properties (predicted change list).
        public WhatIfOperationProperties? Properties { get; set; }

        public override string ToString() => JsonNode.FromObject(this).ToString();
    }
}
