// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity.Customization;

namespace Cli.Tests;

public class IdentityCliArgumentParserTests
{
    [Fact]
    public void ParseTags_Null_ReturnsNull() =>
        Assert.Null(IdentityCliArgumentParser.ParseTags(null));

    [Fact]
    public void ParseTags_EmptyArray_ReturnsNull() =>
        Assert.Null(IdentityCliArgumentParser.ParseTags([]));

    [Fact]
    public void ParseTags_KeyValue_ReturnsDictionary() =>
        Assert.Equal(new Dictionary<string, string> { ["env"] = "test" }, IdentityCliArgumentParser.ParseTags(["env=test"]));

    [Fact]
    public void ParseTags_Json_ReturnsDictionary() =>
        Assert.Equal(new Dictionary<string, string> { ["env"] = "test" }, IdentityCliArgumentParser.ParseTags(["""{"env":"test"}"""]));

    [Fact]
    public void ParseTags_MissingSeparator_ThrowsInvalidInput()
    {
        var ex = Assert.Throws<InvalidInputException>(() => IdentityCliArgumentParser.ParseTags(["novalue"]));

        Assert.Equal("Invalid dictionary entry 'novalue'. Expected KEY=VALUE format.", ex.Message);
    }

    [Theory]
    [InlineData("=value")]
    [InlineData("   =value")]
    public void ParseTags_EmptyKey_ThrowsInvalidInput(string tag)
    {
        var ex = Assert.Throws<InvalidInputException>(() => IdentityCliArgumentParser.ParseTags([tag]));

        Assert.Equal($"Invalid dictionary entry '{tag}'. Keys cannot be empty.", ex.Message);
    }

    [Theory]
    [InlineData("""{"": "value"}""")]
    [InlineData("""{" ": "value"}""")]
    public void ParseTags_JsonEmptyKey_ThrowsInvalidInput(string tag)
    {
        var ex = Assert.Throws<InvalidInputException>(() => IdentityCliArgumentParser.ParseTags([tag]));

        Assert.Equal("Invalid dictionary entry. Keys cannot be empty.", ex.Message);
    }

    [Fact]
    public void ParseResourceRestrictionProviders_Json_ReturnsProviders() =>
        Assert.Equal(["Microsoft.Compute"], IdentityCliArgumentParser.ParseResourceRestrictionProviders(["""{"providers":["Microsoft.Compute"]}"""]));

    [Fact]
    public void ParseResourceRestrictionProviders_CommaSeparated_ReturnsProviders() =>
        Assert.Equal(["Microsoft.Compute", "Microsoft.Web"], IdentityCliArgumentParser.ParseResourceRestrictionProviders(["Microsoft.Compute, Microsoft.Web"]));

    // Malformed JSON surfaces the framework JsonException (a derived type), not a custom message.
    [Fact]
    public void ParseResourceRestrictionProviders_InvalidJson_ThrowsJsonException() =>
        Assert.ThrowsAny<JsonException>(() => IdentityCliArgumentParser.ParseResourceRestrictionProviders(["{bad-json"]));
}
