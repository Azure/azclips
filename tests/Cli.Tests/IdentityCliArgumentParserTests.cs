// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity.Customization;

namespace Cli.Tests;

public class IdentityCliArgumentParserTests
{
    [Fact]
    public void ParseTags_Null_ReturnsNull()
    {
        var tags = IdentityCliArgumentParser.ParseTags(null);

        Assert.Null(tags);
    }

    [Fact]
    public void ParseTags_EmptyArray_ReturnsNull()
    {
        var tags = IdentityCliArgumentParser.ParseTags([]);

        Assert.Null(tags);
    }

    [Fact]
    public void ParseTags_KeyValue_ReturnsDictionary()
    {
        var tags = IdentityCliArgumentParser.ParseTags(["env=test"]);

        Assert.Equal(new Dictionary<string, string> { ["env"] = "test" }, tags);
    }

    [Fact]
    public void ParseTags_Json_ReturnsDictionary()
    {
        var tags = IdentityCliArgumentParser.ParseTags(["""{"env":"test"}"""]);

        Assert.Equal(new Dictionary<string, string> { ["env"] = "test" }, tags);
    }

    [Fact]
    public void ParseTags_MissingSeparator_ThrowsInvalidInput()
    {
        Exception? exception = Record.Exception(() => IdentityCliArgumentParser.ParseTags(["novalue"]));

        Assert.Equal("Invalid dictionary entry 'novalue'. Expected KEY=VALUE format.", Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Theory]
    [InlineData("=value")]
    [InlineData("   =value")]
    public void ParseTags_EmptyKey_ThrowsInvalidInput(string tag)
    {
        Exception? exception = Record.Exception(() => IdentityCliArgumentParser.ParseTags([tag]));

        Assert.Equal($"Invalid dictionary entry '{tag}'. Keys cannot be empty.", Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Theory]
    [InlineData("""{"": "value"}""")]
    [InlineData("""{" ": "value"}""")]
    public void ParseTags_JsonEmptyKey_ThrowsInvalidInput(string tag)
    {
        Exception? exception = Record.Exception(() => IdentityCliArgumentParser.ParseTags([tag]));

        Assert.Equal("Invalid dictionary entry. Keys cannot be empty.", Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public void ParseResourceRestrictionProviders_Json_ReturnsProviders()
    {
        var providers = IdentityCliArgumentParser.ParseResourceRestrictionProviders(["""{"providers":["Microsoft.Compute"]}"""]);

        Assert.Equal(["Microsoft.Compute"], providers);
    }

    [Fact]
    public void ParseResourceRestrictionProviders_CommaSeparated_ReturnsProviders()
    {
        var providers = IdentityCliArgumentParser.ParseResourceRestrictionProviders(["Microsoft.Compute, Microsoft.Web"]);

        Assert.Equal(["Microsoft.Compute", "Microsoft.Web"], providers);
    }

    // Malformed JSON surfaces the framework JsonException (a derived type), not a custom message.
    [Fact]
    public void ParseResourceRestrictionProviders_InvalidJson_ThrowsJsonException()
    {
        Exception? exception = Record.Exception(() => IdentityCliArgumentParser.ParseResourceRestrictionProviders(["{bad-json"]));

        Assert.IsAssignableFrom<JsonException>(exception);
    }
}
