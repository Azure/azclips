// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using Microsoft.Azure.Azclips.ExtensionRuntime;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Models
{
    //
    // Summary:
    //     Result of the request to list virtual machines. ARM list endpoints return an
    //     envelope { "value": [...], "nextLink": "..." }; this wraps that envelope.
    public class VirtualMachineListResult : IArmListPage<VirtualMachine>
    {
        [JsonPropertyName("value")]
        public VirtualMachine[]? Value { get; set; }

        //
        // Summary:
        //     The URL to use for getting the next set of results.
        [JsonPropertyName("nextLink")]
        public string? NextLink { get; set; }
    }
}
