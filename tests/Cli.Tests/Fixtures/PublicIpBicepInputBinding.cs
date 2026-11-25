// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Cli.Tests.Fixtures;

internal static class PublicIpBicepInputBinding
{
    internal static IDictionary<string, DeploymentParameter> Bind(string resourceName, JsonElement normalizedBody)
    {
        if (normalizedBody.ValueKind != JsonValueKind.Object)
            throw new InvalidInputException("The Public IP Bicep pilot requires a normalized request object.");
        if (normalizedBody.TryGetProperty("id", out JsonElement id) && id.ValueKind != JsonValueKind.Null)
            throw new InvalidInputException("Root body 'id' is unsupported by the Public IP Bicep pilot. It cannot be silently discarded.");

        CheckFields(normalizedBody, "$", ["id", "location", "tags", "sku", "properties"]);
        JsonElement sku = RequiredObject(normalizedBody, "sku", "$");
        JsonElement properties = RequiredObject(normalizedBody, "properties", "$");
        CheckFields(sku, "$.sku", ["name", "tier"]);
        CheckFields(properties, "$.properties", ["publicIPAllocationMethod", "publicIPAddressVersion", "idleTimeoutInMinutes"]);

        string skuName = RequiredString(sku, "name", "$.sku");
        string allocation = RequiredString(properties, "publicIPAllocationMethod", "$.properties");
        string version = RequiredString(properties, "publicIPAddressVersion", "$.properties");
        if (!skuName.Equals("Standard", StringComparison.OrdinalIgnoreCase) ||
            !allocation.Equals("Static", StringComparison.OrdinalIgnoreCase) ||
            !version.Equals("IPv4", StringComparison.OrdinalIgnoreCase))
            throw new InvalidInputException("The Public IP Bicep pilot requires Standard, Static, IPv4 normalized inputs.");
        if (!properties.TryGetProperty("idleTimeoutInMinutes", out JsonElement timeout) ||
            timeout.ValueKind != JsonValueKind.Number || !timeout.TryGetInt32(out int minutes))
            throw new InvalidInputException("The Public IP Bicep pilot requires an Int32 value at '$.properties.idleTimeoutInMinutes'.");

        var parameters = new Dictionary<string, DeploymentParameter>(StringComparer.Ordinal)
        {
            ["publicIpAddressName"] = new() { Value = resourceName },
            ["location"] = new() { Value = RequiredString(normalizedBody, "location", "$") },
            ["skuName"] = new() { Value = skuName },
            ["publicIPAllocationMethod"] = new() { Value = allocation },
            ["publicIPAddressVersion"] = new() { Value = version },
            ["idleTimeoutInMinutes"] = new() { Value = minutes },
        };
        if (sku.TryGetProperty("tier", out _))
        {
            string tier = RequiredString(sku, "tier", "$.sku");
            if (!tier.Equals("Regional", StringComparison.OrdinalIgnoreCase))
                throw new InvalidInputException("The Public IP Bicep pilot supports only the Regional tier.");
            parameters["skuTier"] = new() { Value = tier };
        }
        if (normalizedBody.TryGetProperty("tags", out JsonElement tags))
        {
            if (tags.ValueKind != JsonValueKind.Object || tags.EnumerateObject().Any(tag => tag.Value.ValueKind != JsonValueKind.String))
                throw new InvalidInputException("The Public IP Bicep pilot requires string-valued resource tags.");
            parameters["tags"] = new() { Value = tags.Clone() };
        }
        return parameters;
    }

    private static void CheckFields(JsonElement value, string path, string[] allowed)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty property in value.EnumerateObject())
        {
            if (!seen.Add(property.Name))
                throw new InvalidInputException($"Duplicate Public IP Bicep pilot input '{path}.{property.Name}'.");
            if (!allowed.Contains(property.Name, StringComparer.Ordinal))
                throw new InvalidInputException($"Unsupported Public IP Bicep pilot input '{path}.{property.Name}'.");
        }
    }

    private static JsonElement RequiredObject(JsonElement parent, string name, string path)
    {
        if (!parent.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.Object)
            throw new InvalidInputException($"The Public IP Bicep pilot requires an object at '{path}.{name}'.");
        return value;
    }

    private static string RequiredString(JsonElement parent, string name, string path)
    {
        if (!parent.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.String)
            throw new InvalidInputException($"The Public IP Bicep pilot requires a string at '{path}.{name}'.");
        return value.GetString()!;
    }
}
