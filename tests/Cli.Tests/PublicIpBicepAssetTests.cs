// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cli.Tests.Fixtures;
using Core.Abstractions;

namespace Cli.Tests;

public class PublicIpBicepAssetTests
{
    [Theory]
    [InlineData("[]")]
    [InlineData("""{"tags":null}""")]
    [InlineData("""{"tags":"not-an-object"}""")]
    public void Load_InvalidSelectionInputFailsBeforeReadingAssets(string body)
    {
        using var document = JsonDocument.Parse(body);
        Assert.Throws<InvalidInputException>(() => PublicIpBicepAssets.Load(document.RootElement,
            _ => throw new InvalidOperationException("Invalid input must not read any asset.")));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"tags":{}}""")]
    [InlineData("""{"tags":{"Owner":"team-A"}}""")]
    public void Load_SelectsByPresenceAndReturnsUnchangedEmbeddedCompiledJson(string body)
    {
        using var document = JsonDocument.Parse(body);
        bool tagsPresent = document.RootElement.TryGetProperty("tags", out _);
        JsonElement template = PublicIpBicepAssets.Load(document.RootElement);
        string variant = tagsPresent ? "TagsPresent" : "TagsAbsent";
        using Stream expected = PublicIpBicepAssets.OpenResource($"PublicIpBicepCreate.{variant}.json")!;
        using var expectedDocument = JsonDocument.Parse(expected);
        Assert.True(JsonElement.DeepEquals(expectedDocument.RootElement, template));
        JsonElement resource = template.GetProperty("resources").EnumerateObject().Single().Value;
        Assert.Equal("Microsoft.Network/publicIPAddresses", resource.GetProperty("type").GetString());
        Assert.Equal("2026-03-01", resource.GetProperty("apiVersion").GetString());
        Assert.Equal(tagsPresent, resource.TryGetProperty("tags", out _));
        Assert.Equal(tagsPresent, template.GetProperty("parameters").TryGetProperty("tags", out _));
        Assert.True(JsonElement.DeepEquals(template, PublicIpBicepAssets.Load(document.RootElement)));
    }

    [Theory]
    [InlineData("PublicIpBicepCreate.Assets.json")]
    [InlineData("PublicIpBicepCreate.TagsAbsent.json")]
    public void Load_MissingSelectedAssetOrManifestFailsWithoutCompilerFallback(string missing)
    {
        using var body = JsonDocument.Parse("{}");
        var error = Assert.Throws<InvalidOperationException>(() => PublicIpBicepAssets.Load(body.RootElement,
            name => name == missing ? null : PublicIpBicepAssets.OpenResource(name)));
        Assert.Contains("missing", error.Message);
        Assert.Contains(missing, error.Message);
    }

    [Theory]
    [InlineData("digest")]
    [InlineData("extra-resource")]
    [InlineData("dependency")]
    [InlineData("api-version")]
    [InlineData("compiler")]
    [InlineData("config")]
    [InlineData("provenance")]
    [InlineData("missing-digest")]
    [InlineData("invalid-json")]
    public void Load_RejectsInvalidAssetsEvenWhenAChangedTemplateHasAnUpdatedManifestHash(string defect)
    {
        using var body = JsonDocument.Parse("{}");
        var manifest = JsonNode.Parse(Read("PublicIpBicepCreate.Assets.json"))!.AsObject();
        var template = JsonNode.Parse(Read("PublicIpBicepCreate.TagsAbsent.json"))!.AsObject();
        var resource = template["resources"]!["publicIp"]!;
        if (defect == "extra-resource") template["resources"]!["extra"] = resource.DeepClone();
        if (defect == "dependency") resource["dependsOn"] = new JsonArray("other-resource");
        if (defect == "api-version") resource["apiVersion"] = "2026-05-01";
        if (defect == "compiler") manifest["compiler"]!["sha256"] = new string('0', 64);
        if (defect == "config") manifest["compilerConfigSha256"] = new string('0', 64);
        if (defect == "provenance") manifest["source"]!["bindingSha256"] = new string('0', 64);
        if (defect == "missing-digest") manifest["assets"]!["TagsAbsent"]!.AsObject().Remove("sourceSha256");
        byte[] content = Encoding.UTF8.GetBytes(defect == "invalid-json" ? "not-json" : template.ToJsonString());
        if (defect != "digest")
            manifest["assets"]!["TagsAbsent"]!["sha256"] = Convert.ToHexStringLower(SHA256.HashData(content));
        var error = Assert.Throws<InvalidOperationException>(() => PublicIpBicepAssets.Load(body.RootElement, name => name switch
        {
            "PublicIpBicepCreate.Assets.json" => new MemoryStream(Encoding.UTF8.GetBytes(manifest.ToJsonString())),
            "PublicIpBicepCreate.TagsAbsent.json" => new MemoryStream(content),
            _ => PublicIpBicepAssets.OpenResource(name),
        }));
        Assert.Contains("Public IP Bicep asset", error.Message);
    }

    private static string Read(string name)
    {
        using Stream stream = PublicIpBicepAssets.OpenResource(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
