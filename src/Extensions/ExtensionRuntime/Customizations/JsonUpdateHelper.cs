// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Text.Json.Nodes;
using Core.Abstractions;

namespace Microsoft.Azure.Azclips.ExtensionRuntime;

public static class JsonUpdateHelper
{
    public static void MergePatch(JsonObject target, JsonObject patch)
    {
        foreach (var property in patch)
        {
            if (property.Value is JsonObject patchObject
                && target[property.Key] is JsonObject targetObject)
            {
                MergePatch(targetObject, patchObject);
                continue;
            }

            target[property.Key] = property.Value?.DeepClone();
        }
    }

    public static void ApplyGenericUpdate(JsonObject target, string[]? set, string[]? add, string[]? remove, bool forceString)
    {
        foreach (string expression in set ?? [])
        {
            int separator = expression.IndexOf('=');
            if (separator <= 0)
            {
                throw AzclipsErrorException.InvalidInput($"Invalid --set expression '{expression}'. Expected PATH=VALUE.");
            }

            SetValue(target, expression[..separator], ParseLooseCliValue(expression[(separator + 1)..], forceString));
        }

        for (int i = 0; i < (add?.Length ?? 0);)
        {
            string path = add![i++];
            if (i >= add.Length)
            {
                throw AzclipsErrorException.InvalidInput($"Invalid --add expression for path '{path}'. Expected PATH VALUE.");
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
                    throw AzclipsErrorException.InvalidInput($"Invalid --add value '{item}'. Expected KEY=VALUE when adding to an object.");
                }

                obj[item[..separator]] = ParseLooseCliValue(item[(separator + 1)..], forceString);
                continue;
            }

            throw AzclipsErrorException.InvalidInput($"Cannot add to '{path}' because it does not resolve to an array or object.");
        }

        foreach (string path in remove ?? [])
        {
            RemoveValue(target, path);
        }
    }

    public static void RemovePaths(JsonObject target, params string[] paths)
    {
        foreach (string path in paths)
        {
            var segments = SplitPath(path);
            if (segments.Length == 0)
            {
                continue;
            }

            JsonObject? parent = segments.Length == 1
                ? target
                : ResolvePath(target, string.Join('.', segments[..^1]), create: false) as JsonObject;

            parent?.Remove(segments[^1]);
        }
    }

    private static JsonNode? ParseLooseCliValue(string value, bool forceString)
    {
        if (forceString)
        {
            return JsonValue.Create(value);
        }

        if (value.Equals("null", StringComparison.OrdinalIgnoreCase))
        {
            return null;
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
            throw AzclipsErrorException.InvalidInput("Generic update path cannot be empty.");
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
            throw AzclipsErrorException.InvalidInput("Generic update path cannot be empty.");
        }

        JsonNode? parent = ResolvePath(root, string.Join('.', segments[..^1]), create: false);
        if (parent is JsonObject obj)
        {
            obj.Remove(segments[^1]);
            return;
        }

        throw AzclipsErrorException.InvalidInput($"Cannot remove '{path}' because its parent does not exist.");
    }

    private static string[] SplitPath(string path) =>
        path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
