using System.Globalization;
using System.Text.Json;

namespace Core.Cli;

/// <summary>
/// Formats command output values for CLI console display.
/// Strings and primitives are rendered via ToString(); complex objects
/// (including collections and dictionaries) are serialized as JSON.
/// </summary>
internal static class CliOutputFormatter
{
    // No naming policy — preserve the original property casing from the model.
    // C# model properties that carry [JsonPropertyName] attributes will use those
    // names; all others will serialize with their declared C# casing.
    // This avoids a double-transformation (API→model→camelCase) and keeps output
    // faithful to the model definitions.
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Converts a command output value to a display string suitable for the console.
    /// </summary>
    internal static string FormatValue(object? value)
    {
        if (value is null)
            return string.Empty;

        if (value is string s)
            return s;

        // Use InvariantCulture for primitives/decimals so output is locale-independent
        // (e.g., 3.14 not 3,14).
        var type = value.GetType();
        if (type.IsPrimitive || type == typeof(decimal))
            return value is IFormattable fmt
                ? fmt.ToString(null, CultureInfo.InvariantCulture)
                : (value.ToString() ?? string.Empty);

        // TODO: Reconsider error handling before GA. Rethrowing surfaces serialization
        // bugs early. A fallback to ToString() is not useful here because it would most
        // likely return a meaningless type name. Once models stabilise, catching and
        // falling back gracefully may be preferable for production resilience.
        return JsonSerializer.Serialize(value, value.GetType(), s_jsonOptions);
    }
}
