// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>Legacy PS <c>PSResourceManagerError</c>: the <c>Test-Az*Deployment</c> output for a failed validation.</summary>
public class PSResourceManagerError
{
    public string? Code { get; set; }

    public string? Message { get; set; }

    public string? Target { get; set; }

    public List<PSResourceManagerError>? Details { get; set; }

    // Legacy ToPSResourceManagerError: an empty target becomes null; details are converted recursively.
    internal static PSResourceManagerError From(ErrorResponse error) => new()
    {
        Code = error.Code,
        Message = error.Message,
        Target = string.IsNullOrEmpty(error.Target) ? null : error.Target,
        Details = error.Details?.Select(From).ToList(),
    };
}
