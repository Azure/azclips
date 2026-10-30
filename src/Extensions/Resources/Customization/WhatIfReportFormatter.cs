// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// Scope-neutral what-if change report for every deployment scope and both surfaces, like <c>az deployment * what-if</c>
/// and <c>-WhatIf</c>/<c>-Confirm</c>: noise notice, symbol legend, changes by scope, change counts, potential changes and
/// diagnostics, as plain text without colour.
/// </summary>
public static class WhatIfReportFormatter
{
    private const string NoiseNotice =
        "Note: The result may contain false positive predictions (noise).\n" +
        "You can help us improve the accuracy of the result by opening an issue here: https://aka.ms/WhatIfIssues\n" +
        "NOTICE! - Want to get What-if without noise? Move to Deployment Stacks What-If https://aka.ms/stackswhatifGA";

    private sealed record ChangeKind(string Name, string Symbol, int Weight, string? CountLabel = null);

    private static readonly Dictionary<string, ChangeKind> s_changeKinds = new ChangeKind[]
    {
        new("Delete", "-", 0, "to delete"),
        new("Create", "+", 1, "to create"),
        new("Deploy", "!", 2, "to deploy"),
        new("Modify", "~", 3, "to modify"),
        new("NoChange", "=", 4, "no change"),
        new("Unsupported", "x", 5, "unsupported"),
        new("Ignore", "*", 6, "to ignore"),
        new("NoEffect", "x", 7),
    }.ToDictionary(k => k.Name, StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, ChangeKind> s_propertyChangeKinds = new ChangeKind[]
    {
        new("Delete", "-", 0),
        new("Create", "+", 1),
        new("Modify", "~", 2),
        new("Array", "~", 2),
        new("NoEffect", "x", 3),
    }.ToDictionary(k => k.Name, StringComparer.OrdinalIgnoreCase);

    private static readonly Regex s_managementGroupPrefix = new(@"^/?providers/Microsoft\.Management/managementGroups/(?<id>[\w\.\(\)-]+)", RegexOptions.IgnoreCase);
    private static readonly Regex s_subscriptionPrefix = new(@"^/?subscriptions/(?<id>[a-f0-9-]+)", RegexOptions.IgnoreCase);
    private static readonly Regex s_resourceGroupPrefix = new(@"^/resourceGroups/(?<name>[-\w\.\(\)]+)", RegexOptions.IgnoreCase);
    private static readonly Regex s_providersPrefix = new(@"^/providers/(?<relative>.+)$", RegexOptions.IgnoreCase);

    /// <summary>The report, or the result itself when <paramref name="noPrettyPrint"/> is set.</summary>
    public static object ToOutput(WhatIfOperationResult result, bool noPrettyPrint) => noPrettyPrint ? result : Format(result);

    public static string Format(WhatIfOperationResult result)
    {
        List<WhatIfChange> changes = result.Properties?.Changes ?? [];
        List<WhatIfChange> potentialChanges = result.Properties?.PotentialChanges ?? [];
        StringBuilder sb = new();

        sb.Append(NoiseNotice).Append("\n\n");
        // Like Az PowerShell, the legend also covers symbols that only potential changes use.
        AppendLegend(sb, [.. changes, .. potentialChanges]);
        AppendChanges(sb, changes, "The deployment will update the following ");
        sb.Append("\nResource changes: ").Append(changes.Count == 0 ? "no change." : FormatCounts(changes));
        if (potentialChanges.Count > 0)
        {
            AppendChanges(sb, potentialChanges, "The following change MAY OR MAY NOT be deployed to the following ");
            sb.Append("\nPotential changes: ").Append(FormatCounts(potentialChanges));
        }
        AppendDiagnostics(sb, changes.Concat(potentialChanges), result.Properties?.Diagnostics);
        return sb.ToString();
    }

    private static ChangeKind? Kind(string? changeType) =>
        changeType is not null && s_changeKinds.TryGetValue(changeType, out ChangeKind? kind) ? kind : null;

    private static ChangeKind? PropertyKind(string? changeType) =>
        changeType is not null && s_propertyChangeKinds.TryGetValue(changeType, out ChangeKind? kind) ? kind : null;

    private static int Weight(ChangeKind? kind) => kind?.Weight ?? int.MaxValue;

    private static void AppendLegend(StringBuilder sb, List<WhatIfChange> changes)
    {
        if (changes.Count == 0)
        {
            return;
        }

        HashSet<ChangeKind> used = [];
        void AddProperties(List<WhatIfPropertyChange>? propertyChanges)
        {
            foreach (WhatIfPropertyChange change in propertyChanges ?? [])
            {
                // An array change is shown with the Modify symbol.
                string? type = string.Equals(change.PropertyChangeType, "Array", StringComparison.OrdinalIgnoreCase) ? "Modify" : change.PropertyChangeType;
                if (Kind(type) is { } kind)
                {
                    used.Add(kind);
                }
                AddProperties(change.Children);
            }
        }
        foreach (WhatIfChange change in changes)
        {
            if (Kind(change.ChangeType) is { } kind)
            {
                used.Add(kind);
            }
            AddProperties(change.Delta);
        }

        sb.Append("Resource and property changes are indicated with ").Append(used.Count == 1 ? "this symbol:" : "these symbols:").Append('\n');
        foreach (ChangeKind kind in used.OrderBy(k => k.Weight))
        {
            sb.Append("  ").Append(kind.Symbol).Append(' ').Append(kind.Name).Append('\n');
        }
    }

    private static string FormatCounts(List<WhatIfChange> changes) =>
        string.Join(", ", changes
            .Select(c => Kind(c.ChangeType))
            .Where(k => k?.CountLabel is not null)
            .GroupBy(k => k!)
            .OrderBy(g => g.Key.Weight)
            .Select(g => $"{g.Count()} {g.Key.CountLabel}")) + ".";

    private static void AppendChanges(StringBuilder sb, List<WhatIfChange> changes, string intro)
    {
        if (changes.Count == 0)
        {
            return;
        }

        List<IGrouping<string, WhatIfChange>> scopes = changes
            .GroupBy(c => Scope(c).ToUpperInvariant())
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .ToList();
        sb.Append('\n').Append(intro).Append(scopes.Count == 1 ? "scope:" : "scopes:").Append('\n');

        foreach (IGrouping<string, WhatIfChange> scope in scopes)
        {
            sb.Append("\nScope: ").Append(Scope(scope.First())).Append('\n');
            List<WhatIfChange> sorted = scope
                .OrderBy(c => Weight(Kind(c.ChangeType)))
                .ThenBy(c => RelativeId(c), StringComparer.Ordinal)
                .ToList();
            foreach (WhatIfChange change in sorted)
            {
                AppendResourceChange(sb, change, ReferenceEquals(change, sorted[^1]));
            }
        }
    }

    private static void AppendResourceChange(StringBuilder sb, WhatIfChange change, bool isLast)
    {
        if (RelativeId(change) is not { } relativeId)
        {
            return;
        }

        ChangeKind? kind = Kind(change.ChangeType);
        JsonNode? before = ToNode(change.Before);
        JsonNode? after = ToNode(change.After);

        sb.Append("\n  ");
        if (kind is not null)
        {
            sb.Append(kind.Symbol).Append(' ');
        }
        sb.Append(relativeId);
        if (ApiVersion(before, after) is { Length: > 0 } apiVersion)
        {
            sb.Append(" [").Append(apiVersion).Append(']');
        }

        if (kind?.Name == "Create" && IsPresent(after))
        {
            AppendJson(sb, after, "", 0, 2);
        }
        else if (kind?.Name == "Delete" && IsPresent(before))
        {
            AppendJson(sb, before, "", 0, 2);
        }
        else if (change.Delta is { Count: > 0 } delta)
        {
            sb.Append('\n');
            AppendPropertyChanges(sb, SortProperties(delta), 2);
        }
        else if (isLast)
        {
            sb.Append('\n');
        }
    }

    private static List<WhatIfPropertyChange> SortProperties(IEnumerable<WhatIfPropertyChange> changes) => changes
        .OrderBy(c => Weight(PropertyKind(c.PropertyChangeType)))
        .ThenBy(c => c.Path ?? "", StringComparer.Ordinal)
        .ToList();

    private static void AppendPropertyChanges(StringBuilder sb, List<WhatIfPropertyChange> changes, int indent)
    {
        int maxPathLength = changes.Where(ConsidersPathLength).Select(c => c.Path?.Length ?? 0).DefaultIfEmpty(0).Max();
        foreach (WhatIfPropertyChange change in changes)
        {
            AppendPropertyChange(sb, change, maxPathLength, indent);
            sb.Append('\n');
        }
    }

    // Only properties printed on one line take part in aligning their values.
    private static bool ConsidersPathLength(WhatIfPropertyChange change) => PropertyKind(change.PropertyChangeType)?.Name switch
    {
        "Create" => IsLeaf(ToNode(change.After)),
        "Delete" or "Modify" => IsLeaf(ToNode(change.Before)),
        _ => change.Children is not { Count: > 0 },
    };

    private static void AppendPropertyChange(StringBuilder sb, WhatIfPropertyChange change, int maxPathLength, int indent)
    {
        ChangeKind? kind = PropertyKind(change.PropertyChangeType);
        JsonNode? before = ToNode(change.Before);
        JsonNode? after = ToNode(change.After);

        switch (kind?.Name)
        {
            case "Delete":
                AppendPropertyPath(sb, change, kind, before, maxPathLength, indent);
                AppendJson(sb, before, "", 0, indent + 1);
                break;
            case "Modify":
                AppendPropertyPath(sb, change, kind, before, maxPathLength, indent);
                AppendPropertyModify(sb, before, after, change.Children, indent + 1);
                break;
            case "Array":
                AppendPropertyPath(sb, change, kind, change.Children is { Count: > 0 } ? new JsonArray(0) : null, maxPathLength, indent);
                AppendPropertyArray(sb, change, change.Children, indent + 1);
                break;
            default:
                // Create, NoEffect, or a type this formatter does not know: show the after-state.
                AppendPropertyPath(sb, change, kind, after, maxPathLength, indent);
                AppendJson(sb, after, "", 0, indent + 1);
                break;
        }
    }

    private static void AppendPropertyPath(StringBuilder sb, WhatIfPropertyChange change, ChangeKind? kind, JsonNode? value, int maxPathLength, int indent)
    {
        if (string.IsNullOrEmpty(change.Path))
        {
            return;
        }

        int padding = maxPathLength - change.Path.Length + 1;
        if (IsNonEmptyArray(value))
        {
            padding = 1;
        }
        if (IsNonEmptyObject(value) || (kind?.Name == "Modify" && change.Children is { Count: > 0 }))
        {
            padding = 0;
        }

        Indent(sb, indent);
        if (kind is not null)
        {
            sb.Append(kind.Symbol).Append(' ');
        }
        sb.Append(change.Path).Append(':').Append(' ', Math.Max(0, padding));
    }

    private static void AppendPropertyModify(StringBuilder sb, JsonNode? before, JsonNode? after, List<WhatIfPropertyChange>? children, int indent)
    {
        if (children is { Count: > 0 })
        {
            sb.Append("\n\n");
            AppendPropertyChanges(sb, SortProperties(children), indent);
            return;
        }

        AppendJson(sb, before, "", 0, indent);
        if (IsNonEmptyObject(before))
        {
            sb.Append('\n');
            Indent(sb, indent);
        }
        else
        {
            sb.Append(' ');
        }
        sb.Append("=>");
        if (!IsNonEmptyObject(after))
        {
            sb.Append(' ');
        }
        AppendJson(sb, after, "", 0, indent);
        if (!IsLeaf(before) && IsLeaf(after))
        {
            sb.Append('\n');
        }
    }

    private static void AppendPropertyArray(StringBuilder sb, WhatIfPropertyChange parent, List<WhatIfPropertyChange>? children, int indent)
    {
        if (string.IsNullOrEmpty(parent.Path))
        {
            // A nested array has no path of its own, so its bracket starts the line.
            indent--;
            Indent(sb, indent);
        }
        if (children is not { Count: > 0 })
        {
            sb.Append("[]\n");
            return;
        }

        sb.Append("[\n");
        AppendPropertyChanges(sb, children
            .OrderBy(c => int.TryParse(c.Path, out int index) ? index : int.MaxValue)
            .ThenBy(c => Weight(PropertyKind(c.PropertyChangeType)))
            .ToList(), indent);
        Indent(sb, indent);
        sb.Append(']');
    }

    private static void AppendJson(StringBuilder sb, JsonNode? value, string path, int maxPathLength, int indent)
    {
        if (IsLeaf(value))
        {
            AppendJsonPath(sb, path, maxPathLength - path.Length + 1, indent);
            AppendLeaf(sb, value);
        }
        else if (value is JsonArray array)
        {
            AppendJsonPath(sb, path, 1, indent);
            AppendJsonArray(sb, array, indent);
        }
        else
        {
            AppendJsonObject(sb, (JsonObject)value!, path, maxPathLength, indent);
        }
    }

    private static void AppendJsonPath(StringBuilder sb, string path, int padding, int indent)
    {
        if (path.Length == 0)
        {
            return;
        }
        Indent(sb, indent);
        sb.Append(path).Append(':').Append(' ', Math.Max(0, padding));
    }

    private static void AppendLeaf(StringBuilder sb, JsonNode? value)
    {
        switch (value)
        {
            case null:
                sb.Append("null");
                break;
            case JsonArray:
                sb.Append("[]");
                break;
            case JsonObject:
                sb.Append("{}");
                break;
            default:
                sb.Append(value.GetValueKind() switch
                {
                    JsonValueKind.String => $"\"{value.GetValue<string>()}\"",
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    JsonValueKind.Null => "null",
                    _ => value.ToJsonString(),
                });
                break;
        }
    }

    private static void AppendJsonArray(StringBuilder sb, JsonArray array, int indent)
    {
        sb.Append("[\n");
        int lastLeafIndex = 0;
        for (int i = 0; i < array.Count; i++)
        {
            if (IsLeaf(array[i]))
            {
                lastLeafIndex = i;
            }
        }
        int maxPathLength = lastLeafIndex.ToString().Length;

        for (int i = 0; i < array.Count; i++)
        {
            if (array[i] is JsonObject { Count: > 0 } item)
            {
                AppendJsonPath(sb, i.ToString(), 0, indent + 1);
                AppendJsonObject(sb, item, "", 0, indent + 1);
            }
            else
            {
                AppendJson(sb, array[i], i.ToString(), maxPathLength, indent + 1);
            }
            sb.Append('\n');
        }
        Indent(sb, indent);
        sb.Append(']');
    }

    // A root object starts on a new block and aligns its values; nested objects print as dotted paths.
    private static void AppendJsonObject(StringBuilder sb, JsonObject value, string path, int maxPathLength, int indent)
    {
        bool isRoot = path.Length == 0;
        if (isRoot)
        {
            sb.Append("\n\n");
            maxPathLength = MaxObjectPathLength(value);
            indent++;
        }

        foreach ((string key, JsonNode? child) in value)
        {
            AppendJson(sb, child, isRoot ? key : $"{path}.{key}", maxPathLength, indent);
            if (!IsNonEmptyObject(child))
            {
                sb.Append('\n');
            }
        }
    }

    // Array paths are left out so one long array path does not pad every other line.
    private static int MaxObjectPathLength(JsonObject value) => value
        .Where(p => !IsNonEmptyArray(p.Value))
        .Select(p => p.Value is JsonObject { Count: > 0 } child ? p.Key.Length + 1 + MaxObjectPathLength(child) : p.Key.Length)
        .DefaultIfEmpty(0)
        .Max();

    // Test-AzTenantDeployment's diagnostics warning; empty when there are none.
    internal static string FormatDiagnostics(object? diagnostics)
    {
        StringBuilder sb = new();
        AppendDiagnostics(sb, [], diagnostics);
        return sb.ToString().Trim('\n');
    }

    private static void AppendDiagnostics(StringBuilder sb, IEnumerable<WhatIfChange> changes, object? diagnostics)
    {
        List<WhatIfChange> unsupported = changes.Where(c => Kind(c.ChangeType)?.Name == "Unsupported").ToList();
        List<JsonNode?> items = ToNode(diagnostics) is JsonArray array ? array.ToList() : [];
        if (unsupported.Count + items.Count == 0)
        {
            return;
        }

        sb.Append("\n\nDiagnostics (").Append(unsupported.Count + items.Count).Append("): \n");
        foreach (WhatIfChange change in unsupported)
        {
            sb.Append(change.ResourceId).Append(" (Unsupported) ").Append(change.UnsupportedReason).Append('\n');
        }
        foreach (JsonNode? item in items)
        {
            sb.Append(Text(item?["target"])).Append(" (").Append(Text(item?["code"])).Append(") ").Append(Text(item?["message"])).Append('\n');
        }
    }

    private static string? Text(JsonNode? node) => node is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : node?.ToJsonString();

    private static string? ApiVersion(JsonNode? before, JsonNode? after)
    {
        JsonNode? source = IsPresent(before) ? before : IsPresent(after) ? after : null;
        return Text(source is JsonObject state ? state["apiVersion"] : null);
    }

    private static string Scope(WhatIfChange change) => change.ResourceId is { Length: > 0 } id ? SplitResourceId(id).Scope : "Unknown";

    private static string? RelativeId(WhatIfChange change) => change.ResourceId is { Length: > 0 } id ? SplitResourceId(id).RelativeId : null;

    // Splits a resource ID into its deployment scope (root, management group, subscription or resource group) and the
    // resource ID relative to it.
    internal static (string Scope, string RelativeId) SplitResourceId(string resourceId)
    {
        string remaining = resourceId;
        Match managementGroup = s_managementGroupPrefix.Match(remaining);
        remaining = remaining[(managementGroup.Success ? managementGroup.Length : 0)..];
        Match subscription = s_subscriptionPrefix.Match(remaining);
        remaining = remaining[(subscription.Success ? subscription.Length : 0)..];
        Match resourceGroup = s_resourceGroupPrefix.Match(remaining);
        remaining = remaining[(resourceGroup.Success ? resourceGroup.Length : 0)..];
        Match providers = s_providersPrefix.Match(remaining);
        string relativeId = providers.Success ? providers.Groups["relative"].Value : "";

        if (managementGroup.Success)
        {
            string managementGroupId = $"Microsoft.Management/ManagementGroups/{managementGroup.Groups["id"].Value}";
            return relativeId.Length > 0 ? ($"/providers/{managementGroupId}", relativeId) : ("/", managementGroupId);
        }
        if (subscription.Success)
        {
            string subscriptionScope = $"/subscriptions/{subscription.Groups["id"].Value.ToLowerInvariant()}";
            if (resourceGroup.Success)
            {
                string resourceGroupId = $"resourceGroups/{resourceGroup.Groups["name"].Value}";
                return providers.Success ? ($"{subscriptionScope}/{resourceGroupId}", relativeId) : (subscriptionScope, resourceGroupId);
            }
            return (subscriptionScope, relativeId);
        }
        return ("/", relativeId);
    }

    private static JsonNode? ToNode(object? value) => value switch
    {
        null => null,
        JsonNode node => node,
        JsonElement element => JsonNode.Parse(element.GetRawText()),
        _ => JsonSerializer.SerializeToNode(value),
    };

    private static bool IsLeaf(JsonNode? value) => value is null or JsonValue or JsonArray { Count: 0 } or JsonObject { Count: 0 };

    private static bool IsNonEmptyArray(JsonNode? value) => value is JsonArray { Count: > 0 };

    private static bool IsNonEmptyObject(JsonNode? value) => value is JsonObject { Count: > 0 };

    private static bool IsPresent(JsonNode? value) => value is not null && !(value is JsonObject { Count: 0 } or JsonArray { Count: 0 });

    private static void Indent(StringBuilder sb, int level) => sb.Append(' ', 2 * level);
}
