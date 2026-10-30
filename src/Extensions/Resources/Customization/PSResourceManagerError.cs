// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Resources.Models
{
    //
    // Summary:
    //     A deployment validation error as Test-AzDeployment outputs it. Mirrors Az.Resources
    //     PSResourceManagerError.
    public class PSResourceManagerError
    {
        // The error code.
        public string? Code { get; set; }

        // The error message.
        public string? Message { get; set; }

        // The error target.
        public string? Target { get; set; }

        // The error details.
        public List<PSResourceManagerError>? Details { get; set; }

        internal static PSResourceManagerError From(ErrorResponse error) => new()
        {
            Code = error.Code,
            Message = error.Message,
            Target = string.IsNullOrEmpty(error.Target) ? null : error.Target,
            Details = error.Details?.Select(From).ToList(),
        };
    }
}
