// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;

namespace Core.Cli;

/// <summary>
/// Renders serialized JSON as tab-separated values (classic <c>az -o tsv</c> shape). Same shape
/// decisions as <see cref="TableFormatter"/> minus header/column-width logic -- no header row,
/// tab-separated instead of padded.
/// </summary>
internal static class TsvFormatter
{
    internal static string Format(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        return root.ValueKind switch
        {
            JsonValueKind.Array => FormatArray(root),
            JsonValueKind.Object => FormatObject(root),
            _ => JsonCellRenderer.Render(root),
        };
    }

    private static string FormatArray(JsonElement array)
    {
        var items = array.EnumerateArray().ToList();
        if (items.Count == 0)
        {
            return string.Empty;
        }

        if (items[0].ValueKind == JsonValueKind.Object)
        {
            var headers = items[0].EnumerateObject().Select(p => p.Name).ToArray();
            return string.Join(
                "\n",
                items.Select(item => string.Join("\t", headers.Select(h => item.TryGetProperty(h, out var v) ? JsonCellRenderer.Render(v) : string.Empty))));
        }

        return string.Join("\n", items.Select(JsonCellRenderer.Render));
    }

    private static string FormatObject(JsonElement obj) =>
        string.Join("\t", obj.EnumerateObject().Select(p => JsonCellRenderer.Render(p.Value)));
}
