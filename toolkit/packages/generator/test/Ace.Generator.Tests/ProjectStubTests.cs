using System.Xml.Linq;
using Ace.Generator.Model;
using Ace.Generator.Model.Ir;
using Xunit;

namespace Ace.Generator.Tests;

/// <summary>
/// Pins the generated extension <c>.csproj</c> (composed with System.Xml.Linq). The stub is not
/// IR-driven, so a minimal empty IR is enough. Output lands in <c>artifacts/ProjectStub_*/</c>.
/// </summary>
public sealed class ProjectStubTests
{
    private const string RootNamespace = "Azure.ElasticSan";

    private static async Task<string> EmitCsprojAsync(string scenario)
    {
        var writer = new ArtifactWriter(scenario);
        await new CSharpClientGenerator().GenerateAsync(
            new TransformedIR
            {
                SchemaVersion = "0.17.0",
                Commands = [],
                Models = [],
                Operations = [],
                Target = Targets.For(RootNamespace),
            },
            config: null,
            writer.WriteAsync);
        return writer.Files.ProjectFile();
    }

    [Fact]
    public async Task Emits_WellFormed_Sdk_Style_Csproj()
    {
        string csproj = await EmitCsprojAsync(nameof(Emits_WellFormed_Sdk_Style_Csproj));

        XDocument doc = XDocument.Parse(csproj); // throws if not well-formed
        Assert.Equal("Project", doc.Root!.Name.LocalName);
        Assert.Equal("Microsoft.NET.Sdk", doc.Root.Attribute("Sdk")?.Value);
    }

    [Fact]
    public async Task Csproj_Carries_Identity_Import_And_Compile_Only_References()
    {
        string csproj = await EmitCsprojAsync(nameof(Csproj_Carries_Identity_Import_And_Compile_Only_References));
        XDocument doc = XDocument.Parse(csproj);

        Assert.Equal(RootNamespace, doc.Descendants("AssemblyName").Single().Value);
        Assert.Equal(RootNamespace, doc.Descendants("PackageId").Single().Value);
        Assert.Contains(doc.Descendants("Import"), i => i.Attribute("Project")!.Value.Contains("Shared.props"));

        // No version: it is decided at release, not at generation.
        Assert.Empty(doc.Descendants("Version"));

        List<string> references = doc.Descendants("ProjectReference").Select(r => r.Attribute("Include")!.Value).ToList();
        Assert.Contains(references, r => r.EndsWith("ExtensionRuntime.csproj", StringComparison.Ordinal));
        Assert.Contains(references, r => r.EndsWith("Core.Abstractions.csproj", StringComparison.Ordinal));

        // Every reference is compile-only: not copied, no runtime asset.
        Assert.All(doc.Descendants("ProjectReference"), r =>
        {
            Assert.Equal("false", r.Element("Private")!.Value);
            Assert.Equal("runtime", r.Element("ExcludeAssets")!.Value);
        });
    }
}
