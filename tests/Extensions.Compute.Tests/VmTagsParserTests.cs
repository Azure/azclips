// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Microsoft.Azure.Azclips.Extensions.Compute.Processors;

namespace Extensions.Compute.Tests;

public class VmTagsParserTests
{
    [Fact]
    public void KeyValueEntries_BecomeTags()
    {
        var tags = VmTagsParser.ParseTags(["env=prod", "owner=team-a"]);

        Assert.NotNull(tags);
        Assert.Equal(2, tags.Count);
        Assert.Equal("prod", tags["env"]);
        Assert.Equal("team-a", tags["owner"]);
    }

    [Fact]
    public void Key_IsTrimmed()
    {
        var tags = VmTagsParser.ParseTags([" env =prod"]);

        var tag = Assert.Single(tags!);
        Assert.Equal("env", tag.Key);
        Assert.Equal("prod", tag.Value);
    }

    [Fact]
    public void Value_KeepsEverythingAfterTheFirstEquals()
    {
        var tags = VmTagsParser.ParseTags(["filter=a=b"]);

        Assert.Equal("a=b", tags!["filter"]);
    }

    [Fact]
    public void EmptyValue_IsKept()
    {
        var tags = VmTagsParser.ParseTags(["env="]);

        Assert.Equal(string.Empty, tags!["env"]);
    }

    [Fact]
    public void Keys_AreCaseInsensitive_AndTheLastValueWins()
    {
        var tags = VmTagsParser.ParseTags(["Env=dev", "env=prod"]);

        Assert.Single(tags!);
        Assert.Equal("prod", tags!["ENV"]);
    }

    [Fact]
    public void JsonObject_BecomesTags()
    {
        var tags = VmTagsParser.ParseTags(["{\"env\":\"prod\",\"owner\":\"team-a\"}"]);

        Assert.NotNull(tags);
        Assert.Equal(2, tags.Count);
        Assert.Equal("prod", tags["env"]);
        Assert.Equal("team-a", tags["owner"]);
    }

    [Fact]
    public void EntryWithoutEquals_IsRejected()
    {
        var error = Assert.Throws<InvalidOperationException>(() => VmTagsParser.ParseTags(["novalue"]));

        Assert.Equal("Invalid dictionary entry 'novalue'. Expected KEY=VALUE format.", error.Message);
    }

    [Theory]
    [InlineData("=prod")]
    [InlineData(" =prod")]
    public void EmptyKey_IsRejected(string entry)
    {
        var error = Assert.Throws<InvalidOperationException>(() => VmTagsParser.ParseTags([entry]));

        Assert.Equal($"Invalid dictionary entry '{entry}'. Keys cannot be empty.", error.Message);
    }

    [Theory]
    [InlineData("{\"\":\"prod\"}")]
    [InlineData("{\" \":\"prod\"}")]
    public void JsonObjectWithEmptyKey_IsRejected(string json)
    {
        var error = Assert.Throws<InvalidOperationException>(() => VmTagsParser.ParseTags([json]));

        Assert.Equal("Invalid dictionary entry. Keys cannot be empty.", error.Message);
    }

    // Only strict JSON whose values are strings is accepted: no shorthand, no numbers.
    [Theory]
    [InlineData("{env:prod}")]
    [InlineData("{\"env\":1}")]
    public void JsonObjectThatIsNotStrictJsonOfStrings_Throws(string json)
    {
        Assert.ThrowsAny<JsonException>(() => VmTagsParser.ParseTags([json]));
    }

    [Fact]
    public void NullOrEmptyInput_MeansNoTags()
    {
        Assert.Null(VmTagsParser.ParseTags(null));
        Assert.Null(VmTagsParser.ParseTags([]));
    }
}
