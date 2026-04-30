using Cirrus.Autocomplete.Agent;

namespace Autocomplete.Tests;

public class SuggestionParserTests
{
    [Fact]
    public void Parse_ShouldReturnEmpty_WhenInputNullOrWhitespace()
    {
        Assert.Empty(SuggestionParser.Parse(string.Empty, prefix: string.Empty));
        Assert.Empty(SuggestionParser.Parse("   ", prefix: string.Empty));
    }

    [Fact]
    public void Parse_ShouldReturnEmpty_WhenInputIsMalformedJson()
    {
        Assert.Empty(SuggestionParser.Parse("not json", prefix: string.Empty));
        Assert.Empty(SuggestionParser.Parse("{ broken", prefix: string.Empty));
    }

    [Fact]
    public void Parse_ShouldReturnEmpty_WhenItemsArrayMissing()
    {
        string json = """{"foo":"bar"}""";

        Assert.Empty(SuggestionParser.Parse(json, prefix: string.Empty));
    }

    [Fact]
    public void Parse_ShouldReturnItems_WhenJsonWellFormed()
    {
        string json = """
        {
          "items": [
            { "value": "create", "kind": "subcommand", "description": "Create a group" },
            { "value": "delete", "kind": "subcommand" }
          ]
        }
        """;

        List<SuggestionItem> result = SuggestionParser.Parse(json, prefix: string.Empty);

        Assert.Equal(2, result.Count);
        Assert.Equal("create", result[0].Value);
        Assert.Equal("Create a group", result[0].Description);
        Assert.Equal("delete", result[1].Value);
    }

    [Fact]
    public void Parse_ShouldFilterByPrefix_CaseInsensitively()
    {
        string json = """
        { "items": [
            { "value": "create" },
            { "value": "Connect" },
            { "value": "delete" }
        ] }
        """;

        List<SuggestionItem> result = SuggestionParser.Parse(json, prefix: "c");

        Assert.Equal(2, result.Count);
        Assert.Contains(result, i => i.Value == "create");
        Assert.Contains(result, i => i.Value == "Connect");
    }

    [Fact]
    public void Parse_ShouldDedup_CaseInsensitively()
    {
        string json = """
        { "items": [
            { "value": "create" },
            { "value": "Create" },
            { "value": "create" }
        ] }
        """;

        List<SuggestionItem> result = SuggestionParser.Parse(json, prefix: string.Empty);

        Assert.Single(result);
        Assert.Equal("create", result[0].Value);
    }

    [Fact]
    public void Parse_ShouldDropItems_ContainingSpaces()
    {
        string json = """
        { "items": [
            { "value": "create group" },
            { "value": "create" }
        ] }
        """;

        List<SuggestionItem> result = SuggestionParser.Parse(json, prefix: string.Empty);

        Assert.Single(result);
        Assert.Equal("create", result[0].Value);
    }

    [Fact]
    public void Parse_ShouldDropItems_WithEmptyValue()
    {
        string json = """
        { "items": [
            { "value": "" },
            { "value": "  " },
            { "value": "create" }
        ] }
        """;

        List<SuggestionItem> result = SuggestionParser.Parse(json, prefix: string.Empty);

        Assert.Single(result);
        Assert.Equal("create", result[0].Value);
    }

    [Fact]
    public void Parse_ShouldTrimValue_AndPopulateDisplayFromValue_WhenDisplayMissing()
    {
        string json = """
        { "items": [
            { "value": "  create  " }
        ] }
        """;

        List<SuggestionItem> result = SuggestionParser.Parse(json, prefix: string.Empty);

        Assert.Single(result);
        Assert.Equal("create", result[0].Value);
        Assert.Equal("create", result[0].Display);
    }

    [Fact]
    public void Parse_ShouldPreserveExplicitDisplay()
    {
        string json = """
        { "items": [
            { "value": "create", "display": "create — make a new resource group" }
        ] }
        """;

        List<SuggestionItem> result = SuggestionParser.Parse(json, prefix: string.Empty);

        Assert.Single(result);
        Assert.Equal("create — make a new resource group", result[0].Display);
    }
}
