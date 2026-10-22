// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

public class RestProcessor_Cli : RestProcessor_Orchestrator
{
    /// <summary>Include status, headers, raw content, and parsed JSON in CLI output.</summary>
    public bool Full { get; set; }

    protected override bool ParseJsonRegardlessOfContentType => true;

    protected override object? SelectOutput(Models.RestResponse response)
    {
        if (Full)
            return response;

        if (response.Content.Length == 0)
            return null;

        return response.Json.HasValue ? (object)response.Json.Value : response.Content;
    }
}
