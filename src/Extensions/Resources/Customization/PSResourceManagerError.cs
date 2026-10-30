// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.ExtensionRuntime.Json;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Models
{
    //
    // Summary:
    //     A deployment error as the PowerShell deployment cmdlets output it, like legacy Az.Resources PSResourceManagerError.
    public class PSResourceManagerError
    {
        public string? Code { get; set; }

        public string? Message { get; set; }

        public string? Target { get; set; }

        public List<PSResourceManagerError>? Details { get; set; }

        public static PSResourceManagerError From(ErrorResponse error) => new()
        {
            Code = error.Code,
            Message = error.Message,
            Target = string.IsNullOrEmpty(error.Target) ? null : error.Target,
            Details = error.Details?.Select(From).ToList(),
        };

        public override string ToString() => JsonNode.FromObject(this).ToString();
    }
}
