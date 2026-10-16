// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions;
using Core.Extensions;

namespace Core.Extensions.Tests;

public sealed class ExtensionSidecarReaderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"sidecar-reader-{Guid.NewGuid():N}");
    private const string PackageId = "Microsoft.Azure.Azclips.Extensions.Compute";

    public ExtensionSidecarReaderTests() => Directory.CreateDirectory(_root);

    private InstalledExtensionRef WriteSidecar(string json, string installedVersion = "1.0.0")
    {
        var location = Path.Combine(_root, $"{PackageId}.dll");
        File.WriteAllText(Path.ChangeExtension(location, ".commands.json"), json);
        return new InstalledExtensionRef("Compute", installedVersion, location);
    }

    private static string Payload(string version = "1.0.0", string[]? verbs = null, string[]? leaves = null)
        => JsonSerializer.Serialize(new
        {
            schemaVersion = 1, packageId = PackageId, version,
            verbs = verbs ?? ["vm"], leaves = leaves ?? ["vm create"],
        });

    [Theory]
    [InlineData("1.0.0", "1.0")]
    [InlineData("1.0.0+build", "1.0.0")]
    [InlineData("1.0.0", "")]
    public void ReadsRoutingMetadataWithoutLoadingAssembly(string sidecarVersion, string installedVersion)
    {
        var installation = WriteSidecar(Payload(sidecarVersion), installedVersion);
        Assert.True(ExtensionSidecarReader.TryRead(installation, out var routing, out var reason), reason);
        Assert.Equal(PackageId, routing!.PackageId);
        Assert.Equal(installedVersion, routing.ResolvedVersion);
        Assert.Equal(["vm"], routing.Verbs);
        Assert.Equal(["vm create"], routing.Leaves);
        Assert.Equal(installation.Location, routing.Location);
    }

    [Fact]
    public void EmptyContractsAreValid()
    {
        var installation = WriteSidecar(Payload(verbs: [], leaves: []));
        Assert.True(ExtensionSidecarReader.TryRead(installation, out var routing, out var reason), reason);
        Assert.Empty(routing!.Verbs);
        Assert.Empty(routing.Leaves);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"schemaVersion\":2}")]
    [InlineData("{\"schemaVersion\":\"1\"}")]
    public void RejectsMalformedOrUnsupportedSchema(string json)
    {
        var installation = WriteSidecar(json);
        Assert.False(ExtensionSidecarReader.TryRead(installation, out var routing, out var reason));
        Assert.Null(routing);
        Assert.NotEmpty(reason);
    }

    [Theory]
    [InlineData("packageId", "\"Other\"")]
    [InlineData("version", "\"2.0.0\"")]
    [InlineData("version", "\"invalid\"")]
    [InlineData("version", "null")]
    [InlineData("verbs", "null")]
    [InlineData("verbs", "[1]")]
    [InlineData("verbs", "[\"vm create\"]")]
    [InlineData("leaves", "\"vm create\"")]
    [InlineData("leaves", "[null]")]
    [InlineData("leaves", "[\"\"]")]
    [InlineData("leaves", "[\"vm  create\"]")]
    [InlineData("leaves", "[\"vm\\tcreate\"]")]
    [InlineData("leaves", "[\" vm create\"]")]
    [InlineData("leaves", "[\"network create\"]")]
    public void RejectsInvalidFields(string field, string value)
    {
        var fields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(Payload())!;
        fields[field] = JsonSerializer.Deserialize<JsonElement>(value);
        var installation = WriteSidecar(JsonSerializer.Serialize(fields));
        Assert.False(ExtensionSidecarReader.TryRead(installation, out _, out var reason));
        Assert.NotEmpty(reason);
    }

    [Fact]
    public void MissingSidecarRequestsFallback()
    {
        var installation = new InstalledExtensionRef("Compute", "", Path.Combine(_root, $"{PackageId}.dll"));
        Assert.False(ExtensionSidecarReader.TryRead(installation, out _, out var reason));
        Assert.Contains("FileNotFoundException", reason);
    }

    [Fact]
    public void MissingLocationRequestsFallback()
    {
        Assert.False(ExtensionSidecarReader.TryRead(new("Compute", "1.0.0", null), out _, out var reason));
        Assert.Contains("no assembly location", reason);
    }

    [Fact]
    public void UnreadableSidecarRequestsFallback()
    {
        var installation = WriteSidecar(Payload());
        var path = Path.ChangeExtension(installation.Location!, ".commands.json");
        File.Delete(path);
        Directory.CreateDirectory(path);
        Assert.False(ExtensionSidecarReader.TryRead(installation, out _, out var reason));
        Assert.NotEmpty(reason);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
