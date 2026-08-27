// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.ExtensionRuntime.Json;

namespace Microsoft.Azure.Azclips.Extensions.MsGraph.Models;

/// <summary>
/// Wire response from <c>POST /directoryObjects/{id}/checkMemberGroups</c>. Graph
/// returns the subset of the queried group IDs that the directory object is a member of.
/// </summary>
public class CheckMemberGroupsResponse
{
    public string[]? Value { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}
