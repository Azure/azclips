// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;

namespace Core.Cli;

/// <summary>Renders a single JSON scalar value as flat text for a table/tsv cell. Shared by <see cref="TableFormatter"/> and <see cref="TsvFormatter"/> so the two never drift on cell rendering.</summary>
internal static class JsonCellRenderer
{
    internal static string Render(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString() ?? string.Empty,
        JsonValueKind.Number => element.GetRawText(),
        JsonValueKind.True => "True",
        JsonValueKind.False => "False",
        JsonValueKind.Null => string.Empty,
        // Nested object/array normally never reaches here -- callers drop those columns entirely
        // (matches classic az table/tsv). Fallback only for the rare shape where the whole row IS
        // a nested value (e.g. array-of-arrays), where there's no column to drop it from.
        JsonValueKind.Object or JsonValueKind.Array => JsonSerializer.Serialize(element),
        _ => element.GetRawText(),
    };

    /// <summary>Top-level property names whose values are scalars -- classic az table/tsv drop nested object/array columns entirely rather than rendering them inline.</summary>
    internal static string[] GetScalarPropertyNames(JsonElement obj) =>
        obj.EnumerateObject().Where(p => p.Value.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array)).Select(p => p.Name).ToArray();
}
