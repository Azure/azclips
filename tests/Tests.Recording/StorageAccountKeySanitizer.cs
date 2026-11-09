// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Nodes;

namespace Tests.Recording;

public sealed class StorageAccountKeySanitizer : IRecordingSanitizer
{
    public string Name => "storage-account-key";

    public string Sanitize(string value)
    {
        if (!value.Contains("keyName", StringComparison.OrdinalIgnoreCase)) return value;
        JsonNode? node;
        try { node = JsonNode.Parse(value); }
        catch (JsonException) { return value; }
        return Redact(node) ? node!.ToJsonString() : value;
    }

    private static bool Redact(JsonNode? node)
    {
        bool changed = false;
        if (node is JsonObject record)
        {
            string? name = record.Select(item => item.Key)
                .FirstOrDefault(key => key.Equals("keyName", StringComparison.OrdinalIgnoreCase));
            string? value = record.Select(item => item.Key)
                .FirstOrDefault(key => key.Equals("value", StringComparison.OrdinalIgnoreCase));
            if (name is not null && record[name] is JsonValue keyName && keyName.TryGetValue<string>(out _) &&
                value is not null && record[value] is JsonValue keyValue &&
                keyValue.TryGetValue<string>(out var text) && text != RecordingSanitizers.FakeStorageKey)
            {
                record[value] = RecordingSanitizers.FakeStorageKey;
                changed = true;
            }
            foreach (var item in record.ToArray()) changed |= Redact(item.Value);
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array) changed |= Redact(item);
        }
        return changed;
    }
}
