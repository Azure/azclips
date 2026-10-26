// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Nodes;

namespace Tests.Recording;

/// <summary>
/// Trims an ARM list response down to the resources in one resource group. A subscription-wide
/// list returns every resource of that type in the recording subscription, including other
/// people's names, hostnames and settings, and no id-level sanitiser removes those. A scenario that
/// exercises the subscription form opts into this for its own group, so only resources the
/// scenario created reach disk.
/// </summary>
/// <remarks>
/// Only a JSON object with a <c>value</c> array is touched, and only entries carrying an
/// <c>id</c> outside the group are dropped, so URIs, headers, single-resource bodies and
/// resource-group-scoped lists pass through unchanged. That keeps it idempotent for playback,
/// which re-sanitises the recorded side.
/// </remarks>
public sealed class ResourceGroupListSanitizer(string resourceGroupName) : IRecordingSanitizer
{
    private readonly string _scope = $"/resourceGroups/{resourceGroupName}/";

    public string Name => "resource-group-list";

    public string Sanitize(string value)
    {
        if (!value.Contains("\"value\"", StringComparison.Ordinal))
        {
            return value;
        }

        JsonObject? root;
        try
        {
            root = JsonNode.Parse(value) as JsonObject;
        }
        catch (System.Text.Json.JsonException)
        {
            return value;
        }

        if (root?["value"] is not JsonArray items)
        {
            return value;
        }

        var outside = items
            .Where(item => item?["id"] is JsonValue id
                           && id.TryGetValue<string>(out var resourceId)
                           && !resourceId.Contains(_scope, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (outside.Count == 0)
        {
            return value;
        }

        foreach (var item in outside)
        {
            items.Remove(item);
        }

        return root.ToJsonString();
    }
}
