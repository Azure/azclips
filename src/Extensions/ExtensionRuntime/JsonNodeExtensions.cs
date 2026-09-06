// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.ExtensionRuntime;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>
/// Extensions for processors that build request payloads with <see cref="System.Text.Json.Nodes.JsonObject"/>.
/// </summary>
public static partial class JsonNodeExtensions
{
    public static bool HasJsonUpdateOperations(this string[]? set, string[]? add, string[]? remove) =>
        set is { Length: > 0 } || add is { Length: > 0 } || remove is { Length: > 0 };

    public static (string Key, JsonNode? Value) ParseJsonKeyValue(this string pair)
    {
        if (string.IsNullOrWhiteSpace(pair))
        {
            throw new ArgumentException("Invalid inline parameter: the token is empty; expected KEY=VALUE.");
        }

        int separator = pair.IndexOf('=');
        if (separator <= 0)
        {
            throw new ArgumentException($"Invalid inline parameter '{pair}': expected KEY=VALUE with a non-empty key.");
        }

        string key = pair[..separator].Trim();
        if (key.Length == 0)
        {
            throw new ArgumentException($"Invalid inline parameter '{pair}': the key is empty.");
        }

        return (key, pair[(separator + 1)..].ParseJsonFirstValue());
    }

    public static JsonNode? ParseJsonFirstValue(this string raw)
    {
        try
        {
            return JsonNode.Parse(raw);
        }
        catch (JsonException)
        {
            return JsonValue.Create(raw);
        }
    }

    public static void MergePatch(this JsonObject target, JsonObject patch)
    {
        foreach (var property in patch)
        {
            if (property.Value is JsonObject patchObject
                && target[property.Key] is JsonObject targetObject)
            {
                targetObject.MergePatch(patchObject);
                continue;
            }

            target[property.Key] = property.Value?.DeepClone();
        }
    }

    public static void ApplyGenericUpdate(this JsonObject target, string[]? set, string[]? add, string[]? remove, bool forceString)
    {
        foreach (string expression in set ?? [])
        {
            int separator = expression.IndexOf('=');
            if (separator <= 0)
            {
                throw new InvalidOperationException($"Invalid --set expression '{expression}'. Expected PATH=VALUE.");
            }

            SetValue(target, expression[..separator], ParseLooseCliValue(expression[(separator + 1)..], forceString));
        }

        for (int i = 0; i < (add?.Length ?? 0);)
        {
            string path = add![i++];
            if (i >= add.Length)
            {
                throw new InvalidOperationException($"Invalid --add expression for path '{path}'. Expected PATH VALUE.");
            }

            JsonNode? destination = ResolvePath(target, path, create: true);
            if (destination is JsonArray array)
            {
                array.Add(ParseLooseCliValue(add[i++], forceString));
                continue;
            }

            if (destination is JsonObject obj)
            {
                string item = add[i++];
                int separator = item.IndexOf('=');
                if (separator <= 0)
                {
                    throw new InvalidOperationException($"Invalid --add value '{item}'. Expected KEY=VALUE when adding to an object.");
                }

                obj[item[..separator]] = ParseLooseCliValue(item[(separator + 1)..], forceString);
                continue;
            }

            throw new InvalidOperationException($"Cannot add to '{path}' because it does not resolve to an array or object.");
        }

        foreach (string path in remove ?? [])
        {
            RemoveValue(target, path);
        }
    }

    private static JsonNode? ParseLooseCliValue(string value, bool forceString)
    {
        if (forceString)
        {
            return JsonValue.Create(value);
        }

        if (bool.TryParse(value, out bool boolValue))
        {
            return JsonValue.Create(boolValue);
        }

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long longValue))
        {
            return JsonValue.Create(longValue);
        }

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double doubleValue))
        {
            return JsonValue.Create(doubleValue);
        }

        if ((value.StartsWith('{') && value.EndsWith('}')) || (value.StartsWith('[') && value.EndsWith(']')))
        {
            return JsonNode.Parse(value);
        }

        return JsonValue.Create(value);
    }

    private static bool LooksLikePath(string value) =>
        value.Contains('.', StringComparison.Ordinal) && !value.Contains('=', StringComparison.Ordinal);

    private static JsonNode? ResolvePath(JsonObject root, string path, bool create)
    {
        JsonNode current = root;
        foreach (string segment in SplitPath(path))
        {
            if (current is not JsonObject obj)
            {
                return null;
            }

            if (!obj.TryGetPropertyValue(segment, out JsonNode? next) || next is null)
            {
                if (!create)
                {
                    return null;
                }

                next = new JsonObject();
                obj[segment] = next;
            }
            current = next;
        }
        return current;
    }

    private static void SetValue(JsonObject root, string path, JsonNode? value)
    {
        var segments = SplitPath(path);
        if (segments.Length == 0)
        {
            throw new InvalidOperationException("Generic update path cannot be empty.");
        }

        JsonObject parent = root;
        for (int i = 0; i < segments.Length - 1; i++)
        {
            string segment = segments[i];
            if (!parent.TryGetPropertyValue(segment, out JsonNode? next) || next is null)
            {
                next = new JsonObject();
                parent[segment] = next;
            }

            parent = next as JsonObject
                ?? throw new InvalidOperationException($"Cannot set '{path}' because '{segment}' is not an object.");
        }

        parent[segments[^1]] = value;
    }

    private static void RemoveValue(JsonObject root, string path)
    {
        var segments = SplitPath(path);
        if (segments.Length == 0)
        {
            throw new InvalidOperationException("Generic update path cannot be empty.");
        }

        JsonNode? parent = ResolvePath(root, string.Join('.', segments[..^1]), create: false);
        if (parent is JsonObject obj)
        {
            obj.Remove(segments[^1]);
            return;
        }

        throw new InvalidOperationException($"Cannot remove '{path}' because its parent does not exist.");
    }

    private static string[] SplitPath(string path) =>
        path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
