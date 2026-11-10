// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.MsGraph.Processors;
using System.Net;

namespace Extensions.MsGraph.Tests;

public class SelectClauseHelpersTests
{
    [Fact]
    public void RethrowMissingConsistencyLevelAsInvalidInput_MatchingBadRequest_ShouldThrowHelpfulInvalidInput()
    {
        var graphError = new HttpRequestException(
            "400 Bad Request: {\"error\":{\"code\":\"Request_UnsupportedQuery\",\"message\":\"Operator 'endsWith' is not supported because the 'ConsistencyLevel:eventual' header is missing.\"}}",
            inner: null,
            statusCode: HttpStatusCode.BadRequest);

        var ex = Assert.Throws<InvalidInputException>(
            () => SelectClauseHelpers.RethrowMissingConsistencyLevelAsInvalidInput(graphError));
        Assert.Contains("ConsistencyLevel 'eventual'", ex.Message);
        Assert.Same(graphError, ex.InnerException);
    }

    [Fact]
    public void RethrowMissingConsistencyLevelAsInvalidInput_NonMatchingBadRequest_ShouldNotThrow()
    {
        var otherBadRequest = new HttpRequestException(
            "400 Bad Request: {\"error\":{\"code\":\"Request_BadRequest\",\"message\":\"Invalid filter clause.\"}}",
            inner: null,
            statusCode: HttpStatusCode.BadRequest);

        SelectClauseHelpers.RethrowMissingConsistencyLevelAsInvalidInput(otherBadRequest);
    }

    [Fact]
    public void RethrowMissingConsistencyLevelAsInvalidInput_NonBadRequest_ShouldNotThrow()
    {
        var serverError = new HttpRequestException(
            "500 Internal Server Error: upstream service failure",
            inner: null,
            statusCode: HttpStatusCode.InternalServerError);

        SelectClauseHelpers.RethrowMissingConsistencyLevelAsInvalidInput(serverError);
    }

    [Theory]
    [InlineData("simple", "simple")]
    [InlineData("O'Brien", "O''Brien")]
    [InlineData("It's a 'test'", "It''s a ''test''")]
    [InlineData("", "")]
    public void EscapeODataString_ShouldDoubleSingleQuotes(string input, string expected)
    {
        Assert.Equal(expected, SelectClauseHelpers.EscapeODataString(input));
    }

    [Fact]
    public void CombineFilterClauses_AllInputsEmpty_ShouldReturnNull()
    {
        Assert.Null(SelectClauseHelpers.CombineFilterClauses(null));
        Assert.Null(SelectClauseHelpers.CombineFilterClauses(""));
        Assert.Null(SelectClauseHelpers.CombineFilterClauses("   ", null, ""));
    }

    [Fact]
    public void CombineFilterClauses_SingleFilter_ShouldReturnItAsIs()
    {
        Assert.Equal(
            "accountEnabled eq true",
            SelectClauseHelpers.CombineFilterClauses("accountEnabled eq true"));
    }

    [Fact]
    public void CombineFilterClauses_SingleShortcut_ShouldReturnItAsIs()
    {
        Assert.Equal(
            "displayName eq 'Sales'",
            SelectClauseHelpers.CombineFilterClauses(null, "displayName eq 'Sales'"));
    }

    [Fact]
    public void CombineFilterClauses_MultipleInputs_ShouldJoinWithAnd()
    {
        var combined = SelectClauseHelpers.CombineFilterClauses(
            "accountEnabled eq true",
            "displayName eq 'Sales'",
            "startswith(mail,'a')");

        Assert.Equal(
            "accountEnabled eq true and displayName eq 'Sales' and startswith(mail,'a')",
            combined);
    }

    [Fact]
    public void CombineFilterClauses_ShouldSkipWhitespaceAndNullShortcuts()
    {
        var combined = SelectClauseHelpers.CombineFilterClauses(
            "accountEnabled eq true",
            null,
            "   ",
            "displayName eq 'Sales'");

        Assert.Equal("accountEnabled eq true and displayName eq 'Sales'", combined);
    }

    [Fact]
    public void MergeUserSelect_NoSelectNoAppend_ShouldReturnNull()
    {
        Assert.Null(SelectClauseHelpers.MergeUserSelect(null, appendSelected: false));
        Assert.Null(SelectClauseHelpers.MergeUserSelect(Array.Empty<string>(), appendSelected: false));
    }

    [Fact]
    public void MergeUserSelect_SelectOnly_ShouldReturnSelectVerbatim()
    {
        var result = SelectClauseHelpers.MergeUserSelect(
            new[] { "id", "displayName" },
            appendSelected: false);

        Assert.Equal(new[] { "id", "displayName" }, result);
    }

    [Fact]
    public void MergeUserSelect_AppendSelectedOnly_ShouldReturnDefaults()
    {
        var result = SelectClauseHelpers.MergeUserSelect(null, appendSelected: true);

        Assert.NotNull(result);
        Assert.Contains("id", result);
        Assert.Contains("displayName", result);
        Assert.Contains("userPrincipalName", result);
    }

    [Fact]
    public void MergeUserSelect_AppendSelectedWithExtras_ShouldPrependDefaults()
    {
        var result = SelectClauseHelpers.MergeUserSelect(
            new[] { "jobTitle" },
            appendSelected: true);

        Assert.NotNull(result);
        Assert.Contains("id", result);
        Assert.Contains("jobTitle", result);
        // Extras follow defaults.
        Assert.Equal("jobTitle", result![^1]);
    }

    [Fact]
    public void MergeUserSelect_AppendSelectedWithDuplicate_ShouldDeduplicate()
    {
        var result = SelectClauseHelpers.MergeUserSelect(
            new[] { "displayName", "jobTitle" },
            appendSelected: true);

        Assert.NotNull(result);
        Assert.Equal(1, result!.Count(static v => string.Equals(v, "displayName", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void MergeGroupSelect_AppendSelectedOnly_ShouldReturnGroupDefaults()
    {
        var result = SelectClauseHelpers.MergeGroupSelect(null, appendSelected: true);

        Assert.NotNull(result);
        Assert.Contains("id", result);
        Assert.Contains("displayName", result);
    }

    [Fact]
    public void MergeGroupSelect_SelectOnly_ShouldReturnSelectVerbatim()
    {
        var result = SelectClauseHelpers.MergeGroupSelect(
            new[] { "id", "mailNickname" },
            appendSelected: false);

        Assert.Equal(new[] { "id", "mailNickname" }, result);
    }
}
