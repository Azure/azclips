// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using Core.Abstractions;
using Core.Cli.Parser;

namespace Core.Cli.Output;

/// <summary>
/// Renders a single <c>config get</c> result for a list-valued configuration key. The readable
/// view is used only with the built-in default format and no <c>--query</c>; otherwise the
/// entries are formatted as an array in the requested format.
/// </summary>
internal static class ConfigListRenderer
{
    internal const string EmptyListMessage = "  empty — AI recovery runs for all commands";

    public static bool TryRender(
        object? value,
        Func<string, bool> isListKey,
        Func<OutputFormatSource> getFormatSource,
        OutputFormat format,
        OutputFormat simpleFormat,
        bool colorPolicyAllowsColor,
        string? query,
        int? terminalWidth,
        out string? text)
    {
        text = null;
        if (!TryGetConfigEntry(value, out var key, out var entryValue, out var entrySource) || !isListKey(key))
        {
            return false;
        }

        var entries = CommandExclusionList.Split(entryValue?.ToString()).ToArray();

        if (query is null && getFormatSource() == OutputFormatSource.Default)
        {
            var builder = new StringBuilder();
            builder.Append(key).Append(" [").Append(entrySource).Append(']');
            if (entries.Length == 0)
            {
                builder.AppendLine().Append(EmptyListMessage);
            }
            else
            {
                foreach (var entry in entries)
                {
                    builder.AppendLine().Append("  - ").Append(entry);
                }
            }

            text = builder.ToString();
            return true;
        }

        text = CliOutputFormatter.FormatValue(entries, format, simpleFormat, colorPolicyAllowsColor, query, terminalWidth);
        return true;
    }

    // The CLI host does not reference the Config extension, so its ConfigEntry record is matched by shape.
    private static bool TryGetConfigEntry(object? value, out string key, out object? entryValue, out string entrySource)
    {
        key = string.Empty;
        entryValue = null;
        entrySource = string.Empty;
        if (value is null)
        {
            return false;
        }

        var type = value.GetType();
        if (type.Name != "ConfigEntry")
        {
            return false;
        }

        var keyProperty = type.GetProperty("Key");
        var valueProperty = type.GetProperty("Value");
        var sourceProperty = type.GetProperty("Source");
        if (keyProperty?.GetValue(value) is not string keyValue || valueProperty is null || sourceProperty is null)
        {
            return false;
        }

        key = keyValue;
        entryValue = valueProperty.GetValue(value);
        entrySource = sourceProperty.GetValue(value)?.ToString() ?? string.Empty;
        return true;
    }
}
