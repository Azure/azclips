// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime.Json;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Models
{
    //
    // Summary:
    //     A deployment operation as the PowerShell deployment operation cmdlets output it.
    public class PSDeploymentOperation
    {
        // Full deployment operation ID.
        public string? Id { get; set; }

        // Deployment operation ID.
        public string? OperationId { get; set; }

        // The state of the provisioning.
        public string? ProvisioningState { get; set; }

        // Operation status code from the resource provider.
        public string? StatusCode { get; set; }

        // The error reported by the operation, rendered as text.
        public string? StatusMessage { get; set; }

        // The Azure resource ID of the resource the operation targeted.
        public string? TargetResource { get; set; }

        public static PSDeploymentOperation From(DeploymentOperation operation) => new()
        {
            Id = operation.Id,
            OperationId = operation.OperationId,
            ProvisioningState = operation.Properties?.ProvisioningState,
            StatusCode = operation.Properties?.StatusCode,
            StatusMessage = operation.Properties?.StatusMessage?.Error is { } error ? DeploymentOperationStatusMessage.Format(error) : null,
            TargetResource = operation.Properties?.TargetResource?.Id,
        };

        public override string ToString() => JsonNode.FromObject(this).ToString();
    }

    internal static class DeploymentOperationStatusMessage
    {
        internal static string Format(ErrorResponse error) => string.Join("\n", FormatLines(error, 0));

        private static IEnumerable<string> FormatLines(ErrorResponse error, int detailLevel)
        {
            if (error.Details is not { Count: > 0 } details)
            {
                yield return $"{error.Message} (Code:{error.Code})";
                yield break;
            }

            yield return $"{error.Message} (Code: {error.Code})";

            string prefix = new string(' ', detailLevel * 2) + " - ";
            foreach (ErrorResponse detail in details)
            {
                bool first = true;
                foreach (string line in FormatLines(detail, detailLevel + 1))
                {
                    // Only a detail's own line takes this level's prefix; its nested detail lines already carry theirs.
                    yield return first ? prefix + line : line;
                    first = false;
                }
            }
        }
    }

    internal static class PSDeploymentOperationOutput
    {
        /// <summary>
        /// Wraps an output writer so a written set of <see cref="DeploymentOperation"/> is passed on as
        /// <see cref="PSDeploymentOperation"/> in the same order; any other value passes through unchanged.
        /// </summary>
        internal static OutputStreamDelegate Project(OutputStreamDelegate writeOutput) =>
            (value, cancellationToken) => writeOutput(
                value is IEnumerable<DeploymentOperation> operations
                    ? operations.Select(PSDeploymentOperation.From).ToList()
                    : value,
                cancellationToken);
    }
}
