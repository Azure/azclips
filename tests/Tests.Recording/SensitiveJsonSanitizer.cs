// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Nodes;

namespace Tests.Recording;

public sealed class SensitiveJsonSanitizer : IRecordingSanitizer
{
    public string Name => "sensitive-json";

    public string Sanitize(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        try
        {
            JsonNode? node = JsonNode.Parse(value);
            if (node is not JsonObject && node is not JsonArray) return "[redacted]";
            Redact(node);
            return node.ToJsonString();
        }
        catch (JsonException) { return "[redacted]"; }
    }

    private static void Redact(JsonNode? node)
    {
        if (node is JsonObject record)
        {
            foreach (string key in record.Select(item => item.Key).ToArray())
            {
                if (key.Equals("error", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("pwd", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("password", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("value", StringComparison.OrdinalIgnoreCase) && record[key] is not JsonArray)
                    record[key] = "[redacted]";
                else Redact(record[key]);
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array) Redact(item);
        }
    }
}
