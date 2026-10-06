// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using Microsoft.Azure.Azclips.Extensions.Compute.Processors;

namespace Extensions.Compute.Tests;

/// <summary>
/// Unit tests for <c>ResourceTags</c>, which parses vm create's tags the way each legacy tool
/// does: <c>az vm create --tags</c> tokens like azure-cli's <c>validate_tags</c>, and the
/// <c>New-AzVM -Tag</c> hashtable like azure-powershell's
/// <c>TagsConversionHelper.CreateTagDictionary</c>.
/// </summary>
public class ResourceTagsTests
{
    [Fact]
    public void Null_MeansNoTags()
    {
        Assert.Null(ResourceTags.Normalize(null));
    }

    [Fact]
    public void Tokens_AreKeyValuePairs()
    {
        AssertTags(new() { ["env"] = "prod", ["owner"] = "team-a" }, new List<string> { "env=prod", "owner=team-a" });
    }

    [Fact]
    public void Tokens_KeyWithoutAValue_GetsAnEmptyValue()
    {
        AssertTags(new() { ["owner"] = "" }, new List<string> { "owner" });
    }

    [Fact]
    public void Tokens_SplitOnTheFirstEquals()
    {
        AssertTags(new() { ["team"] = "a=b" }, new List<string> { "team=a=b" });
    }

    [Fact]
    public void Tokens_AreNotTrimmed()
    {
        AssertTags(new() { [" a "] = " b " }, new List<string> { " a = b " });
    }

    [Fact]
    public void Tokens_EmptyKey_IsLeftForArmToJudge()
    {
        AssertTags(new() { [""] = "v" }, new List<string> { "=v" });
    }

    [Fact]
    public void Tokens_LastDuplicateWins()
    {
        AssertTags(new() { ["env"] = "b" }, new List<string> { "env=a", "env=b" });
    }

    [Fact]
    public void Tokens_KeysAreCaseSensitive()
    {
        AssertTags(new() { ["Env"] = "a", ["env"] = "b" }, new List<string> { "Env=a", "env=b" });
    }

    [Fact]
    public void Tokens_EmptyTokens_AreSkipped()
    {
        AssertTags(new() { ["env"] = "prod" }, new List<string> { "", "env=prod" });
    }

    [Fact]
    public void Tokens_None_GiveEmptyTags()
    {
        AssertTags(new(), new List<string>());
    }

    [Fact]
    public void Tokens_OnlyAnEmptyToken_GivesEmptyTags()
    {
        AssertTags(new(), new List<string> { "" });
    }

    [Fact]
    public void Tokens_AsAStringArray_AreAccepted()
    {
        AssertTags(new() { ["env"] = "prod" }, new[] { "env=prod" });
    }

    [Fact]
    public void Hashtable_KeysAndValues_GoThroughToString()
    {
        AssertTags(new() { ["env"] = "prod", ["count"] = "5" }, new Hashtable { ["env"] = "prod", ["count"] = 5 });
    }

    [Fact]
    public void Hashtable_NullValue_BecomesEmpty()
    {
        AssertTags(new() { ["owner"] = "" }, new Hashtable { ["owner"] = null });
    }

    [Fact]
    public void Hashtable_Empty_GivesEmptyTags()
    {
        AssertTags(new(), new Hashtable());
    }

    [Fact]
    public void Dictionary_IsAccepted()
    {
        AssertTags(new() { ["env"] = "prod" }, new Dictionary<string, string> { ["env"] = "prod" });
    }

    [Fact]
    public void UnsupportedType_Throws()
    {
        var error = Assert.Throws<ArgumentException>(() => ResourceTags.Normalize(42));

        Assert.Equal("Tags must be KEY[=VALUE] strings or a dictionary, not System.Int32.", error.Message);
    }

    // xUnit compares dictionaries by content: key casing counts, order does not.
    private static void AssertTags(Dictionary<string, string> expected, object input)
    {
        Dictionary<string, string>? actual = ResourceTags.Normalize(input);

        Assert.NotNull(actual);
        Assert.Equal(expected, actual);
    }
}
