// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.ExtensionRuntime.Json;

namespace Microsoft.Azure.Azclips.Extensions.MsGraph.Models;

/// <summary>
/// Wire request body for <c>POST /directoryObjects/{id}/checkMemberGroups</c>: the group object
/// IDs to test the directory object's transitive membership against.
/// </summary>
public class CheckMemberGroupsRequest
{
    public string[] GroupIds { get; set; } = [];

    public override string ToString() => JsonNode.FromObject(this).ToString();
}
