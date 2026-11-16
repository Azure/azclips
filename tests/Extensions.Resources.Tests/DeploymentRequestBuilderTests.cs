// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;

namespace Extensions.Resources.Tests;

/// <summary>Direct tests for the scope-neutral deployment input rules in <see cref="DeploymentRequestBuilder"/>.</summary>
public sealed class DeploymentRequestBuilderTests : IDisposable
{
    private const string TemplateUri = "https://example.com/t.json";

    private readonly List<string> _tempFiles = [];

    public void Dispose() => _tempFiles.ForEach(File.Delete);

    [Fact]
    public void ValidateCommonInputs_NameWithInvalidCharacter_ThrowsInvalidInput()
    {
        DeploymentRequestBuilder builder = Builder(b => b.Name = "bad name!");

        var ex = Assert.Throws<InvalidInputException>(builder.ValidateCommonInputs);

        Assert.Equal(@"Deployment name must be 1-64 characters and match the pattern ^[-\w\._\(\)]+$.", ex.Message);
    }

    [Fact]
    public void ValidateCommonInputs_SixtyFiveCharacterName_ThrowsInvalidInput()
    {
        DeploymentRequestBuilder builder = Builder(b => b.Name = new string('a', 65));

        var ex = Assert.Throws<InvalidInputException>(builder.ValidateCommonInputs);

        Assert.Equal(@"Deployment name must be 1-64 characters and match the pattern ^[-\w\._\(\)]+$.", ex.Message);
    }

    [Fact]
    public void ValidateCommonInputs_SixtyFourCharacterName_DoesNotThrow()
    {
        DeploymentRequestBuilder builder = Builder(b => b.Name = new string('a', 64));

        Assert.Null(Record.Exception(builder.ValidateCommonInputs));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateCommonInputs_BlankName_DefaultsToTemplateName(string? name)
    {
        DeploymentRequestBuilder builder = Builder(b =>
        {
            b.Name = name!;
            b.TemplateUri = "https://example.com/templates/main.json";
        });

        builder.ValidateCommonInputs();

        Assert.Equal("main", builder.Name);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(TemplateUri, "t.json")]
    public void ValidateCommonInputs_TemplateSourceCountNotOne_ThrowsArgumentException(string? templateUri, string? templateFile)
    {
        DeploymentRequestBuilder builder = Builder(b =>
        {
            b.TemplateUri = templateUri;
            b.TemplateFile = templateFile;
        });

        var ex = Assert.Throws<ArgumentException>(builder.ValidateCommonInputs);

        Assert.Equal(
            "Exactly one of TemplateUri, TemplateFile, TemplateSpecId, or TemplateObject must be provided, unless ParametersFile is a .bicepparam file that carries its own template.",
            ex.Message);
    }

    [Fact]
    public void ValidateCommonInputs_BicepParamFileWithoutTemplate_DoesNotThrow()
    {
        DeploymentRequestBuilder builder = Builder(b =>
        {
            b.TemplateUri = null;
            b.ParametersFile = "main.bicepparam";
        });

        Assert.Null(Record.Exception(builder.ValidateCommonInputs));
    }

    [Fact]
    public void ValidateCommonInputs_ParametersUriAndParametersFile_ThrowsInvalidInput()
    {
        DeploymentRequestBuilder builder = Builder(b =>
        {
            b.ParametersUri = "https://example.com/p.json";
            b.ParametersFile = "p.json";
        });

        var ex = Assert.Throws<InvalidInputException>(builder.ValidateCommonInputs);

        Assert.Equal("Provide at most one of ParametersUri or ParametersFile.", ex.Message);
    }

    [Fact]
    public void ValidateCommonInputs_InlineParametersWithParametersUri_ThrowsInvalidInput()
    {
        DeploymentRequestBuilder builder = Builder(b =>
        {
            b.ParametersUri = "https://example.com/p.json";
            b.Parameter = ["p1=v1"];
        });

        var ex = Assert.Throws<InvalidInputException>(builder.ValidateCommonInputs);

        Assert.Equal(
            "Inline parameters cannot be combined with a parameters URI; ARM does not allow inline parameters together with a parameters link. Provide a parameters file instead, or remove the parameters URI.",
            ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateCommonInputs_BlankInlineParameter_ThrowsInvalidInput(string token)
    {
        DeploymentRequestBuilder builder = Builder(b => b.Parameter = [token]);

        var ex = Assert.Throws<InvalidInputException>(builder.ValidateCommonInputs);

        Assert.Equal("Invalid inline parameter: the token is empty; expected KEY=VALUE.", ex.Message);
    }

    [Theory]
    [InlineData("novalue")]
    [InlineData("=value")]
    public void ValidateCommonInputs_InlineParameterWithoutKey_ThrowsInvalidInput(string token)
    {
        DeploymentRequestBuilder builder = Builder(b => b.Parameter = [token]);

        var ex = Assert.Throws<InvalidInputException>(builder.ValidateCommonInputs);

        Assert.Equal($"Invalid inline parameter '{token}': expected KEY=VALUE with a non-empty key.", ex.Message);
    }

    [Fact]
    public void ValidateCommonInputs_InlineParameterWithWhitespaceKey_ThrowsInvalidInput()
    {
        DeploymentRequestBuilder builder = Builder(b => b.Parameter = ["   =value"]);

        var ex = Assert.Throws<InvalidInputException>(builder.ValidateCommonInputs);

        Assert.Equal("Invalid inline parameter '   =value': the key is empty.", ex.Message);
    }

    [Theory]
    [InlineData("@")]
    [InlineData("")]
    public void ValidateCommonInputs_InlineParametersFileNotJsonObject_ThrowsArgumentException(string prefix)
    {
        string file = TempFile("[1, 2]");
        DeploymentRequestBuilder builder = Builder(b => b.Parameter = [prefix + file]);

        var ex = Assert.Throws<ArgumentException>(builder.ValidateCommonInputs);

        Assert.Equal($"The parameters file '{file}' must contain a JSON object.", ex.Message);
    }

    [Fact]
    public void ValidateCommonInputs_InlineParametersFileMissing_ThrowsFileNotFound()
    {
        string missing = MissingPath();
        DeploymentRequestBuilder builder = Builder(b => b.Parameter = ["@" + missing]);

        var ex = Assert.Throws<FileNotFoundException>(builder.ValidateCommonInputs);

        Assert.Equal($"File not found: {missing}", ex.Message);
    }

    [Fact]
    public void ValidateCommonInputs_QueryStringWithoutTemplateUri_ThrowsArgumentException()
    {
        DeploymentRequestBuilder builder = Builder(b =>
        {
            b.TemplateUri = null;
            b.TemplateSpecId = "/subscriptions/x/resourceGroups/rg/providers/Microsoft.Resources/templateSpecs/s/versions/1";
            b.QueryString = "sv=2024&sig=abc";
        });

        var ex = Assert.Throws<ArgumentException>(builder.ValidateCommonInputs);

        Assert.Equal("QueryString requires TemplateUri; it is only carried on the template link built from a template URI.", ex.Message);
    }

    [Fact]
    public void ValidateCommonInputs_InvalidMode_ThrowsInvalidInput()
    {
        DeploymentRequestBuilder builder = Builder(b => b.Mode = "Partial");

        var ex = Assert.Throws<InvalidInputException>(builder.ValidateCommonInputs);

        Assert.Equal("Mode must be either 'Incremental' or 'Complete'.", ex.Message);
    }

    [Fact]
    public void ValidateCommonInputs_ModeInDifferentCase_DoesNotThrow()
    {
        DeploymentRequestBuilder builder = Builder(b => b.Mode = "complete");

        Assert.Null(Record.Exception(builder.ValidateCommonInputs));
    }

    [Fact]
    public void BuildTemplate_TemplateFileMissing_ThrowsFileNotFound()
    {
        string missing = MissingPath();
        DeploymentRequestBuilder builder = Builder(b =>
        {
            b.TemplateUri = null;
            b.TemplateFile = missing;
        });

        var ex = Assert.Throws<FileNotFoundException>(() => builder.BuildTemplate(out _));

        Assert.Equal($"File not found: {missing}", ex.Message);
    }

    [Fact]
    public void BuildParameters_ParametersFileNotJsonObjectWithInlineParameters_ThrowsInvalidInput()
    {
        string file = TempFile("[1, 2]");
        DeploymentRequestBuilder builder = Builder(b =>
        {
            b.ParametersFile = file;
            b.Parameter = ["p1=v1"];
        });

        var ex = Assert.Throws<InvalidInputException>(() => builder.BuildParameters(out _));

        Assert.Equal(
            "The parameters file must contain a JSON object of parameters to combine with inline parameter values.",
            ex.Message);
    }

    [Fact]
    public void BuildParameters_RequiredTemplateParametersMissing_ThrowsArgumentException()
    {
        string template = TempFile("""{"parameters":{"zeta":{"type":"string"},"alpha":{"type":"string"},"withDefault":{"type":"string","defaultValue":"d"}},"resources":[]}""");
        DeploymentRequestBuilder builder = Builder(b =>
        {
            b.TemplateUri = null;
            b.TemplateFile = template;
        });
        builder.BuildTemplate(out _);

        var ex = Assert.Throws<ArgumentException>(() => builder.BuildParameters(out _));

        Assert.Equal(
            "Missing value for required template parameter(s): alpha, zeta. Supply them with a parameters file or with inline parameters.",
            ex.Message);
    }

    // Valid inputs unless configure overrides one of them.
    private static DeploymentRequestBuilder Builder(Action<DeploymentRequestBuilder> configure)
    {
        var builder = new DeploymentRequestBuilder { Name = "dep", TemplateUri = TemplateUri };
        configure(builder);
        return builder;
    }

    private string TempFile(string content)
    {
        string path = Path.Combine(Path.GetTempPath(), $"azclips-builder-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, content);
        _tempFiles.Add(path);
        return path;
    }

    private static string MissingPath() =>
        Path.Combine(Path.GetTempPath(), $"azclips-missing-{Guid.NewGuid():N}.json");
}
