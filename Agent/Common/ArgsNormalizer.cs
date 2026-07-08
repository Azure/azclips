using System.Text.Json;

namespace Core.Agent.Common;

/// <summary>
/// The Copilot SDK sometimes hands hook code the tool args as a JSON object
/// (the normal case) and sometimes as a JSON-encoded string of an object
/// (observed for the built-in powershell tool). Every arg-reader in the hook
/// pipeline used to repeat the same pattern: check for JsonElement, if it's a
/// String, try to parse it, then walk it as an object. This helper centralizes
/// the normalization so each reader can focus on the field it wants.
/// </summary>
internal static class ArgsNormalizer
{
    /// <summary>
    /// Normalizes the raw tool-args value into a JSON Object element ready
    /// for property lookup, or <c>null</c> when the input isn't an object
    /// (or isn't a JSON-encoded string of one).
    /// </summary>
    public static JsonElement? AsObject(object? args)
    {
        if (args is not JsonElement je) return null;

        if (je.ValueKind == JsonValueKind.String)
        {
            try
            {
                using var parsed = JsonDocument.Parse(je.GetString()!);
                if (parsed.RootElement.ValueKind == JsonValueKind.Object)
                    return parsed.RootElement.Clone();
                return null;
            }
            catch
            {
                return null;
            }
        }
        return je.ValueKind == JsonValueKind.Object ? je : null;
    }

    /// <summary>
    /// Reads a single string field from a normalized tool-args value.
    /// Returns <c>null</c> when the input isn't an object, the field is
    /// absent, or the field isn't a string.
    /// </summary>
    public static string? TryGetString(object? args, string field)
    {
        var obj = AsObject(args);
        if (obj is null) return null;
        return obj.Value.TryGetProperty(field, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;
    }

    /// <summary>
    /// Reads a single JSON array field from a normalized tool-args value,
    /// returning a detached clone so callers can walk it after any parse
    /// document has been disposed. Returns <c>false</c> when the input
    /// isn't an object, the field is absent, or the field isn't an array.
    /// </summary>
    public static bool TryGetArray(object? args, string field, out JsonElement array)
    {
        array = default;
        var obj = AsObject(args);
        if (obj is null) return false;
        if (!obj.Value.TryGetProperty(field, out var v) || v.ValueKind != JsonValueKind.Array)
            return false;
        // Clone so the array outlives any transient JsonDocument used to parse
        // a JSON-encoded string variant of the args.
        array = v.Clone();
        return true;
    }
}
