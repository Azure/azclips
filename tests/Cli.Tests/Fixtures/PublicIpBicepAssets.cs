// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Core.Abstractions;

namespace Cli.Tests.Fixtures;

internal static class PublicIpBicepAssets
{
    internal static JsonElement Load(JsonElement normalizedBody) => Load(normalizedBody, OpenResource);

    internal static Stream? OpenResource(string name) => typeof(PublicIpBicepAssets).Assembly
        .GetManifestResourceStream($"Cli.Tests.Fixtures.{name}");

    internal static JsonElement Load(JsonElement normalizedBody, Func<string, Stream?> openResource)
    {
        if (normalizedBody.ValueKind != JsonValueKind.Object)
            throw new InvalidInputException("The Public IP Bicep asset requires a normalized request object.");
        bool tagsPresent = normalizedBody.TryGetProperty("tags", out JsonElement tags);
        if (tagsPresent && tags.ValueKind != JsonValueKind.Object)
            throw new InvalidInputException("The Public IP Bicep asset requires object-valued tags when present.");
        string variant = tagsPresent ? "TagsPresent" : "TagsAbsent";
        byte[] bridgeBytes = Read("PublicIpBicepBindings.json", openResource);
        JsonElement bridge = Parse("bridge", bridgeBytes);
        JsonElement pin = Parse("compiler pin", Read("PublicIpBicepCompilerPin.json", openResource));
        JsonElement manifest = Parse("manifest", Read("PublicIpBicepCreate.Assets.json", openResource));
        Ensure(Get(manifest, "schemaVersion").GetString() == "1.0.0", "unsupported manifest schema");
        Ensure(Get(manifest, "textHashNormalization").GetString() == "utf8-lf", "unsupported text hash normalization");
        Ensure(Get(manifest, "compilerConfigHashNormalization").GetString() == "raw-bytes", "unsupported config hash normalization");
        Ensure(Get(manifest, "compilerConfigSha256").GetString() == Convert.ToHexStringLower(SHA256.HashData(
            Read("PublicIpBicepCompilerConfig.json", openResource))), "compiler configuration digest mismatch");
        CheckDigest(Get(manifest, "compilerAssociationSha256"));
        Ensure(Get(manifest, "resourceType").GetString() == "Microsoft.Network/publicIPAddresses@2026-03-01" &&
            JsonElement.DeepEquals(Get(manifest, "resourceType"), Get(bridge, "resourceType")), "resource/version mismatch");
        Ensure(Get(manifest, "bridgeSha256").GetString() == HashText(bridgeBytes), "bridge digest mismatch");
        Ensure(JsonElement.DeepEquals(Get(manifest, "source"), Get(bridge, "provenance")), "source provenance mismatch");
        Ensure(JsonElement.DeepEquals(Get(manifest, "catalogue"), Get(pin, "catalogue")), "catalogue pin mismatch");
        JsonElement compiler = Get(manifest, "compiler");
        string revision = Get(pin, "revision").GetString()
            ?? throw new InvalidOperationException("The Public IP Bicep asset has no compiler source revision.");
        Ensure(revision.Length == 40, "invalid compiler source revision");
        Ensure(Get(compiler, "sha256").GetString() == Get(Get(pin, "asset"), "sha256").GetString() &&
            Get(compiler, "revision").GetString() == revision &&
            Get(compiler, "asset").GetString() == Get(Get(pin, "asset"), "url").GetString() &&
            Get(compiler, "version").GetString() == $"Bicep CLI version {Get(pin, "version").GetString()} ({revision[..10]})",
            "compiler identity mismatch");
        JsonElement assets = Get(manifest, "assets");
        Ensure(assets.ValueKind == JsonValueKind.Object &&
            assets.EnumerateObject().Select(property => property.Name).Order().SequenceEqual(new[] { "TagsAbsent", "TagsPresent" }),
            "invalid variant set");
        JsonElement asset = Get(assets, variant);
        CheckDigest(Get(asset, "sourceSha256"));
        string name = $"PublicIpBicepCreate.{variant}.json";
        Ensure(Get(asset, "fileName").GetString() == name, "unexpected variant filename");
        JsonElement compilation = Get(asset, "compilation");
        Ensure(Get(compilation, "exitCode").GetInt32() == 0 &&
            Get(compilation, "diagnostics").ValueKind == JsonValueKind.Array &&
            Get(compilation, "diagnostics").GetArrayLength() == 0, "unqualified compilation");
        byte[] content = Read(name, openResource);
        Ensure(Get(asset, "sha256").GetString() == Convert.ToHexStringLower(SHA256.HashData(content)), "template digest mismatch");
        JsonElement template = Parse(name, content);
        Ensure(Get(template, "$schema").GetString() == "https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#" &&
            Get(template, "languageVersion").GetString() == "2.0", "unsupported template format/scope");
        Ensure(template.EnumerateObject().All(property => new[] {
            "$schema", "languageVersion", "contentVersion", "metadata", "parameters", "resources" }.Contains(property.Name)),
            "unexpected template orchestration");
        JsonElement resources = Get(template, "resources");
        Ensure(resources.ValueKind == JsonValueKind.Object && resources.EnumerateObject().Count() == 1,
            "exactly one Public IP must be declared");
        JsonElement resource = resources.EnumerateObject().Single().Value;
        Ensure($"{Get(resource, "type").GetString()}@{Get(resource, "apiVersion").GetString()}" == Get(manifest, "resourceType").GetString(),
            "unexpected resource/version");
        Ensure(!resource.EnumerateObject().Any(property =>
            new[] { "dependsOn", "resources", "scope", "parent", "copy", "condition", "existing" }.Contains(property.Name)),
            "dependencies, nested resources or alternate scope are not allowed");
        Ensure(resource.TryGetProperty("tags", out _) == tagsPresent, "root tags presence differs from the selected variant");
        return template;
    }

    private static byte[] Read(string name, Func<string, Stream?> openResource)
    {
        using Stream stream = openResource(name)
            ?? throw new InvalidOperationException($"The compiled Public IP Bicep asset '{name}' is missing.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static JsonElement Parse(string name, byte[] content)
    {
        try
        {
            using var document = JsonDocument.Parse(content);
            return document.RootElement.Clone();
        }
        catch (JsonException error)
        {
            throw new InvalidOperationException($"The Public IP Bicep asset '{name}' contains invalid JSON.", error);
        }
    }

    private static JsonElement Get(JsonElement parent, string name) =>
        parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out JsonElement value) ? value
            : throw new InvalidOperationException($"The Public IP Bicep asset is missing metadata '{name}'.");

    private static string HashText(byte[] content) => Convert.ToHexStringLower(SHA256.HashData(
        Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(content).Replace("\r\n", "\n", StringComparison.Ordinal))));

    private static void CheckDigest(JsonElement value)
    {
        string? digest = value.GetString();
        Ensure(digest is { Length: 64 } && digest.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f'),
            "missing or invalid provenance digest");
    }

    private static void Ensure(bool condition, string reason)
    {
        if (!condition)
            throw new InvalidOperationException($"The compiled Public IP Bicep asset is invalid: {reason}.");
    }
}
