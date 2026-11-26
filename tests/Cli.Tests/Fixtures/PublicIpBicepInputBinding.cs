// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Cli.Tests.Fixtures;

internal static class PublicIpBicepInputBinding
{
    private static readonly Lazy<JsonElement> Bridge = new(ReadBridge);

    internal static IDictionary<string, DeploymentParameter> Bind(string resourceName, JsonElement normalizedBody) =>
        Bind(resourceName, normalizedBody, Bridge.Value);

    internal static JsonElement ReadBridge()
    {
        using Stream stream = typeof(PublicIpBicepInputBinding).Assembly
            .GetManifestResourceStream("Cli.Tests.Fixtures.PublicIpBicepBindings.json")
            ?? throw new InvalidOperationException("The generated Public IP Bicep binding fixture is missing.");
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.Clone();
    }

    internal static IDictionary<string, DeploymentParameter> Bind(string resourceName, JsonElement normalizedBody, JsonElement bridge)
    {
        if (bridge.GetProperty("schemaVersion").GetString() != "1.0.0" ||
            bridge.GetProperty("resourceType").GetString() != "Microsoft.Network/publicIPAddresses@2026-03-01")
            throw new InvalidOperationException("The generated Public IP Bicep binding fixture has an unsupported schema or resource version.");
        var bindings = bridge.GetProperty("bindings").EnumerateArray().Select(binding => new GeneratedBinding(
            binding.GetProperty("role").GetString() ?? throw new InvalidOperationException("A generated binding role is missing."),
            binding.GetProperty("eligibility").GetString() ?? throw new InvalidOperationException("Generated binding eligibility is missing."),
            ReadPath(binding),
            binding.TryGetProperty("deploymentParameter", out JsonElement parameter) ? parameter.GetString() : null)).ToArray();
        GeneratedBinding nameBinding = bindings.Single(binding => binding.Role == "resourceName");
        GeneratedBinding[] fields = bindings.Where(binding => binding.Role == "field" && binding.Eligibility == "eligible").ToArray();
        if (fields.Any(binding => binding.WirePath.Length == 0))
            throw new InvalidOperationException("An eligible generated Public IP binding has no containing wire path.");
        string[] parameterNames = fields.Prepend(nameBinding).Select(ParameterName).ToArray();
        if (parameterNames.Distinct(StringComparer.Ordinal).Count() != parameterNames.Length)
            throw new InvalidOperationException("Generated Public IP deployment parameter names are ambiguous.");

        if (normalizedBody.ValueKind != JsonValueKind.Object)
            throw new InvalidInputException("The Public IP Bicep pilot requires a normalized request object.");
        if (normalizedBody.TryGetProperty("id", out JsonElement id) && id.ValueKind != JsonValueKind.Null)
            throw new InvalidInputException("Root body 'id' is unsupported by the Public IP Bicep pilot. It cannot be silently discarded.");

        string[][] paths = fields.Select(binding => binding.WirePath)
            .Concat(bindings.Where(binding => binding.Role == "computedIdentity").Select(binding => binding.WirePath)).ToArray();
        CheckFields(normalizedBody, "$", paths, 0);
        _ = RequiredString(normalizedBody, "location", "$");
        JsonElement sku = RequiredObject(normalizedBody, "sku", "$");
        JsonElement properties = RequiredObject(normalizedBody, "properties", "$");

        string skuName = RequiredString(sku, "name", "$.sku");
        string allocation = RequiredString(properties, "publicIPAllocationMethod", "$.properties");
        string version = RequiredString(properties, "publicIPAddressVersion", "$.properties");
        if (!skuName.Equals("Standard", StringComparison.OrdinalIgnoreCase) ||
            !allocation.Equals("Static", StringComparison.OrdinalIgnoreCase) ||
            !version.Equals("IPv4", StringComparison.OrdinalIgnoreCase))
            throw new InvalidInputException("The Public IP Bicep pilot requires Standard, Static, IPv4 normalized inputs.");
        if (!properties.TryGetProperty("idleTimeoutInMinutes", out JsonElement timeout) ||
            timeout.ValueKind != JsonValueKind.Number || !timeout.TryGetInt32(out _))
            throw new InvalidInputException("The Public IP Bicep pilot requires an Int32 value at '$.properties.idleTimeoutInMinutes'.");

        if (sku.TryGetProperty("tier", out _))
        {
            string tier = RequiredString(sku, "tier", "$.sku");
            if (!tier.Equals("Regional", StringComparison.OrdinalIgnoreCase))
                throw new InvalidInputException("The Public IP Bicep pilot supports only the Regional tier.");
        }
        if (normalizedBody.TryGetProperty("tags", out JsonElement tags))
        {
            if (tags.ValueKind != JsonValueKind.Object || tags.EnumerateObject().Any(tag => tag.Value.ValueKind != JsonValueKind.String))
                throw new InvalidInputException("The Public IP Bicep pilot requires string-valued resource tags.");
        }
        var parameters = new Dictionary<string, DeploymentParameter>(StringComparer.Ordinal)
        {
            [ParameterName(nameBinding)] = new() { Value = resourceName },
        };
        foreach (GeneratedBinding field in fields)
        {
            JsonElement value = normalizedBody;
            bool present = true;
            foreach (string segment in field.WirePath)
            {
                if (!value.TryGetProperty(segment, out value))
                {
                    present = false;
                    break;
                }
            }
            if (present)
                parameters.Add(ParameterName(field), new() { Value = value.Clone() });
        }
        return parameters;
    }

    private sealed record GeneratedBinding(string Role, string Eligibility, string[] WirePath, string? DeploymentParameter);

    private static string ParameterName(GeneratedBinding binding) =>
        !string.IsNullOrEmpty(binding.DeploymentParameter) ? binding.DeploymentParameter
            : throw new InvalidOperationException("A generated Public IP deployment parameter name is missing.");

    private static string[] ReadPath(JsonElement binding) =>
        binding.TryGetProperty("wirePath", out JsonElement path)
            ? path.EnumerateArray().Select(segment => segment.GetString()
                ?? throw new InvalidOperationException("A generated Public IP wire path segment is missing.")).ToArray()
            : [];

    private static void CheckFields(JsonElement value, string path, string[][] allowed, int depth)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty property in value.EnumerateObject())
        {
            if (!seen.Add(property.Name))
                throw new InvalidInputException($"Duplicate Public IP Bicep pilot input '{path}.{property.Name}'.");
            string[][] matches = allowed.Where(segments => segments.Length > depth && segments[depth] == property.Name).ToArray();
            if (matches.Length == 0)
                throw new InvalidInputException($"Unsupported Public IP Bicep pilot input '{path}.{property.Name}'.");
            if (!matches.Any(segments => segments.Length == depth + 1))
            {
                if (property.Value.ValueKind != JsonValueKind.Object)
                    throw new InvalidInputException($"The Public IP Bicep pilot requires an object at '{path}.{property.Name}'.");
                CheckFields(property.Value, $"{path}.{property.Name}", matches, depth + 1);
            }
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
