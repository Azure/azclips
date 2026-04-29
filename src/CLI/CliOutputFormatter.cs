using System.Text.Json;

namespace Core.Cli;

/// <summary>
/// Formats command output values for CLI console display.
/// Strings and primitives are rendered via ToString(); complex objects
/// (including collections and dictionaries) are serialized as JSON.
/// </summary>
internal static class CliOutputFormatter
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
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

        var type = value.GetType();
        if (type.IsPrimitive || type == typeof(decimal))
            return value.ToString() ?? string.Empty;

        try
        {
            return JsonSerializer.Serialize(value, value.GetType(), s_jsonOptions);
        }
        catch (JsonException)
        {
            return value.ToString() ?? string.Empty;
        }
        catch (NotSupportedException)
        {
            return value.ToString() ?? string.Empty;
        }
    }
}
