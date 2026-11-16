// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;

namespace Extensions.Resources.Tests;

/// <summary>Direct tests for the scope-neutral deployment input rules in <see cref="DeploymentRequestBuilder"/>.</summary>
public sealed class DeploymentRequestBuilderTests : IDisposable
{
    private const string TemplateUri = "https://example.com/t.json";

    private const string InvalidNameMessage = @"Deployment name must be 1-64 characters and match the pattern ^[-\w\._\(\)]+$.";

    private readonly List<string> _tempFiles = [];

    public void Dispose() => _tempFiles.ForEach(File.Delete);

    [Fact]
    public void ValidateCommonInputs_NameWithInvalidCharacter_ThrowsInvalidInput()
    {
        var builder = new DeploymentRequestBuilder { Name = "bad name!", TemplateUri = TemplateUri };

        Exception? exception = Record.Exception(builder.ValidateCommonInputs);

        Assert.Equal(InvalidNameMessage, Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public void ValidateCommonInputs_SixtyFiveCharacterName_ThrowsInvalidInput()
    {
        var builder = new DeploymentRequestBuilder { Name = new string('a', 65), TemplateUri = TemplateUri };

        Exception? exception = Record.Exception(builder.ValidateCommonInputs);

        Assert.Equal(InvalidNameMessage, Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public void ValidateCommonInputs_SixtyFourCharacterName_DoesNotThrow()
    {
        var builder = new DeploymentRequestBuilder { Name = new string('a', 64), TemplateUri = TemplateUri };

        Exception? exception = Record.Exception(builder.ValidateCommonInputs);

        Assert.Null(exception);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateCommonInputs_BlankName_DefaultsToTemplateName(string? name)
    {
        var builder = new DeploymentRequestBuilder { Name = name!, TemplateUri = "https://example.com/templates/main.json" };

        builder.ValidateCommonInputs();

        Assert.Equal("main", builder.Name);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(TemplateUri, "t.json")]
    public void ValidateCommonInputs_TemplateSourceCountNotOne_ThrowsArgumentException(string? templateUri, string? templateFile)
    {
        var builder = new DeploymentRequestBuilder { Name = "dep", TemplateUri = templateUri, TemplateFile = templateFile };

        Exception? exception = Record.Exception(builder.ValidateCommonInputs);

        Assert.Equal(
            "Exactly one of TemplateUri, TemplateFile, TemplateSpecId, or TemplateObject must be provided, unless ParametersFile is a .bicepparam file that carries its own template.",
            Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public void ValidateCommonInputs_BicepParamFileWithoutTemplate_DoesNotThrow()
    {
        var builder = new DeploymentRequestBuilder { Name = "dep", ParametersFile = "main.bicepparam" };

        Exception? exception = Record.Exception(builder.ValidateCommonInputs);

        Assert.Null(exception);
    }

    [Fact]
    public void ValidateCommonInputs_ParametersUriAndParametersFile_ThrowsInvalidInput()
    {
        var builder = new DeploymentRequestBuilder
        {
            Name = "dep",
            TemplateUri = TemplateUri,
            ParametersUri = "https://example.com/p.json",
            ParametersFile = "p.json",
        };

        Exception? exception = Record.Exception(builder.ValidateCommonInputs);

        Assert.Equal("Provide at most one of ParametersUri or ParametersFile.", Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public void ValidateCommonInputs_InlineParametersWithParametersUri_ThrowsInvalidInput()
    {
        var builder = new DeploymentRequestBuilder
        {
            Name = "dep",
            TemplateUri = TemplateUri,
            ParametersUri = "https://example.com/p.json",
            Parameter = ["p1=v1"],
        };

        Exception? exception = Record.Exception(builder.ValidateCommonInputs);

        Assert.Equal(
            "Inline parameters cannot be combined with a parameters URI; ARM does not allow inline parameters together with a parameters link. Provide a parameters file instead, or remove the parameters URI.",
            Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateCommonInputs_BlankInlineParameter_ThrowsInvalidInput(string token)
    {
        var builder = new DeploymentRequestBuilder { Name = "dep", TemplateUri = TemplateUri, Parameter = [token] };

        Exception? exception = Record.Exception(builder.ValidateCommonInputs);

        Assert.Equal("Invalid inline parameter: the token is empty; expected KEY=VALUE.", Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Theory]
    [InlineData("novalue")]
    [InlineData("=value")]
    public void ValidateCommonInputs_InlineParameterWithoutKey_ThrowsInvalidInput(string token)
    {
        var builder = new DeploymentRequestBuilder { Name = "dep", TemplateUri = TemplateUri, Parameter = [token] };

        Exception? exception = Record.Exception(builder.ValidateCommonInputs);

        Assert.Equal(
            $"Invalid inline parameter '{token}': expected KEY=VALUE with a non-empty key.",
            Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public void ValidateCommonInputs_InlineParameterWithWhitespaceKey_ThrowsInvalidInput()
    {
        var builder = new DeploymentRequestBuilder { Name = "dep", TemplateUri = TemplateUri, Parameter = ["   =value"] };

        Exception? exception = Record.Exception(builder.ValidateCommonInputs);

        Assert.Equal("Invalid inline parameter '   =value': the key is empty.", Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Theory]
    [InlineData("@")]
    [InlineData("")]
    public void ValidateCommonInputs_InlineParametersFileNotJsonObject_ThrowsArgumentException(string prefix)
    {
        string file = TempFile("[1, 2]");
        var builder = new DeploymentRequestBuilder { Name = "dep", TemplateUri = TemplateUri, Parameter = [prefix + file] };

        Exception? exception = Record.Exception(builder.ValidateCommonInputs);

        Assert.Equal($"The parameters file '{file}' must contain a JSON object.", Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public void ValidateCommonInputs_InlineParametersFileMissing_ThrowsFileNotFound()
    {
        string missing = MissingPath();
        var builder = new DeploymentRequestBuilder { Name = "dep", TemplateUri = TemplateUri, Parameter = ["@" + missing] };

        Exception? exception = Record.Exception(builder.ValidateCommonInputs);

        Assert.Equal($"File not found: {missing}", Assert.IsType<FileNotFoundException>(exception).Message);
    }

    [Fact]
    public void ValidateCommonInputs_QueryStringWithoutTemplateUri_ThrowsArgumentException()
    {
        var builder = new DeploymentRequestBuilder
        {
            Name = "dep",
            TemplateSpecId = "/subscriptions/x/resourceGroups/rg/providers/Microsoft.Resources/templateSpecs/s/versions/1",
            QueryString = "sv=2024&sig=abc",
        };

        Exception? exception = Record.Exception(builder.ValidateCommonInputs);

        Assert.Equal(
            "QueryString requires TemplateUri; it is only carried on the template link built from a template URI.",
            Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public void ValidateCommonInputs_InvalidMode_ThrowsInvalidInput()
    {
        var builder = new DeploymentRequestBuilder { Name = "dep", TemplateUri = TemplateUri, Mode = "Partial" };

        Exception? exception = Record.Exception(builder.ValidateCommonInputs);

        Assert.Equal("Mode must be either 'Incremental' or 'Complete'.", Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public void ValidateCommonInputs_ModeInDifferentCase_DoesNotThrow()
    {
        var builder = new DeploymentRequestBuilder { Name = "dep", TemplateUri = TemplateUri, Mode = "complete" };

        Exception? exception = Record.Exception(builder.ValidateCommonInputs);

        Assert.Null(exception);
    }

    [Fact]
    public void BuildTemplate_TemplateFileMissing_ThrowsFileNotFound()
    {
        string missing = MissingPath();
        var builder = new DeploymentRequestBuilder { Name = "dep", TemplateFile = missing };

        Exception? exception = Record.Exception(() => builder.BuildTemplate(out _));

        Assert.Equal($"File not found: {missing}", Assert.IsType<FileNotFoundException>(exception).Message);
    }

    [Fact]
    public void BuildParameters_ParametersFileNotJsonObjectWithInlineParameters_ThrowsInvalidInput()
    {
        string file = TempFile("[1, 2]");
        var builder = new DeploymentRequestBuilder { Name = "dep", TemplateUri = TemplateUri, ParametersFile = file, Parameter = ["p1=v1"] };

        Exception? exception = Record.Exception(() => builder.BuildParameters(out _));

        Assert.Equal(
            "The parameters file must contain a JSON object of parameters to combine with inline parameter values.",
            Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public void BuildParameters_RequiredTemplateParametersMissing_ThrowsArgumentException()
    {
        string template = TempFile("""{"parameters":{"zeta":{"type":"string"},"alpha":{"type":"string"},"withDefault":{"type":"string","defaultValue":"d"}},"resources":[]}""");
        var builder = new DeploymentRequestBuilder { Name = "dep", TemplateFile = template };
        builder.BuildTemplate(out _);

        Exception? exception = Record.Exception(() => builder.BuildParameters(out _));

        Assert.Equal(
            "Missing value for required template parameter(s): alpha, zeta. Supply them with a parameters file or with inline parameters.",
            Assert.IsType<ArgumentException>(exception).Message);
    }

    private string TempFile(string content)
    {
        string path = Path.Combine(Path.GetTempPath(), $"azclips-builder-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, content);
        _tempFiles.Add(path);
        return path;
    }

    private string MissingPath() =>
        Path.Combine(Path.GetTempPath(), $"azclips-missing-{Guid.NewGuid():N}.json");
}
