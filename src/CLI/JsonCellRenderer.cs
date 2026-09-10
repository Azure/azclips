// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;

namespace Core.Cli;

/// <summary>Renders a single JSON scalar/complex value as flat text for a table/tsv cell. Shared by <see cref="TableFormatter"/> and <see cref="TsvFormatter"/> so the two never drift on cell rendering.</summary>
internal static class JsonCellRenderer
{
    internal static string Render(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString() ?? string.Empty,
        JsonValueKind.Number => element.GetRawText(),
        JsonValueKind.True => "True",
        JsonValueKind.False => "False",
        JsonValueKind.Null => string.Empty,
        // Nested object/array: compact inline JSON (matches classic az table/tsv), not the
        // original indented text -- a cell can't contain embedded newlines without breaking
        // row alignment.
        JsonValueKind.Object or JsonValueKind.Array => JsonSerializer.Serialize(element),
        _ => element.GetRawText(),
    };
}
