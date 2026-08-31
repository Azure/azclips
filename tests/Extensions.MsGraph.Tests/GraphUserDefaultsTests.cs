// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.MsGraph;

namespace Extensions.MsGraph.Tests;

public class GraphUserDefaultsTests
{
    [Fact]
    public void DefaultSelectProperties_ShouldMatchAzResourcesSurface()
    {
        // Regression guard: reordering or renaming entries silently changes wire output
        // for every caller of -AppendSelected.
        Assert.Equal(
            new[]
            {
                "displayName",
                "id",
                "deletedDateTime",
                "userPrincipalName",
                "usageLocation",
                "givenName",
                "surname",
                "accountEnabled",
                "mailNickname",
                "mail",
            },
            GraphUserDefaults.DefaultSelectProperties);
    }

    [Fact]
    public void BuildSelectClause_NoSelectNoAppend_ShouldReturnNull()
    {
        Assert.Null(GraphUserDefaults.BuildSelectClause(select: null, appendSelected: false));
        Assert.Null(GraphUserDefaults.BuildSelectClause(select: Array.Empty<string>(), appendSelected: false));
    }

    [Fact]
    public void BuildSelectClause_SelectOnly_ShouldPassThroughVerbatim()
    {
        var result = GraphUserDefaults.BuildSelectClause(new[] { "city", "department" }, appendSelected: false);

        Assert.Equal("city,department", result);
    }

    [Fact]
    public void BuildSelectClause_AppendSelectedOnly_ShouldReturnDefaults()
    {
        var result = GraphUserDefaults.BuildSelectClause(select: null, appendSelected: true);

        Assert.Equal(string.Join(',', GraphUserDefaults.DefaultSelectProperties), result);
    }

    [Fact]
    public void BuildSelectClause_AppendSelectedWithExtras_ShouldPrependDefaults()
    {
        var result = GraphUserDefaults.BuildSelectClause(new[] { "city" }, appendSelected: true);

        Assert.StartsWith("displayName,id,deletedDateTime,userPrincipalName,usageLocation,givenName,surname,accountEnabled,mailNickname,mail", result, StringComparison.Ordinal);
        Assert.EndsWith(",city", result, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildSelectClause_AppendSelectedWithDuplicate_ShouldDeduplicateCaseInsensitively()
    {
        // Graph rejects duplicate $select entries; the casing already added via the defaults wins.
        var result = GraphUserDefaults.BuildSelectClause(new[] { "DisplayName", "City" }, appendSelected: true);

        var fields = result!.Split(',');
        Assert.Single(fields, static f => string.Equals(f, "displayName", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("displayName", fields.First(static f => string.Equals(f, "displayName", StringComparison.OrdinalIgnoreCase)));
        Assert.Contains("City", fields);
    }

    [Fact]
    public void BuildSelectClause_ShouldDropEmptyAndWhitespaceEntries()
    {
        var result = GraphUserDefaults.BuildSelectClause(new[] { "city", "", "   ", "department" }, appendSelected: false);

        Assert.Equal("city,department", result);
    }

    [Fact]
    public void BuildSelectClause_ShouldTrimSurroundingWhitespaceOnEntries()
    {
        var result = GraphUserDefaults.BuildSelectClause(new[] { "city", " city ", "  department" }, appendSelected: false);

        Assert.Equal("city,department", result);
    }
}
