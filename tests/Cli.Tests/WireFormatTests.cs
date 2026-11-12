// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using Microsoft.Azure.Azclips.ExtensionRuntime;

namespace Cli.Tests;

/// <summary>
/// Tests for <see cref="WireFormat"/>, which generated processors use to put path, query,
/// and header values on the wire.
/// </summary>
public class WireFormatTests
{
    // ── Format ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("de-DE")]
    [InlineData("sv-SE")]
    public void Format_IgnoresTheCurrentCulture(string culture)
    {
        // de-DE writes a decimal comma and sv-SE a U+2212 minus sign; neither may reach the wire.
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);

            Assert.Equal("0.5", WireFormat.Format(0.5));
            Assert.Equal("1234.5", WireFormat.Format(1234.5m));
            Assert.Equal("-1", WireFormat.Format(-1));
            Assert.Equal("-9007199254740993", WireFormat.Format(-9007199254740993L));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void Format_WritesAFloatInItsShortestRoundTripForm()
    {
        Assert.Equal("0.1", WireFormat.Format(0.1f));
        Assert.Equal("1E+20", WireFormat.Format(1e20));
    }

    [Fact]
    public void Format_SpellsABooleanInLowercase()
    {
        Assert.Equal("true", WireFormat.Format(true));
        Assert.Equal("false", WireFormat.Format(false));
    }

    [Fact]
    public void Format_EncodesBytesAsBase64()
    {
        Assert.Equal("AQID", WireFormat.Format(new byte[] { 1, 2, 3 }));
    }

    [Fact]
    public void Format_KeepsAStringAndRendersNullEmpty()
    {
        Assert.Equal("a b", WireFormat.Format("a b"));
        Assert.Equal(string.Empty, WireFormat.Format(null));
    }

    // ── WithQuery ─────────────────────────────────────────────────────────────

    [Fact]
    public void WithQuery_LeavesANullValueOff()
    {
        const string uri = "https://example.com/widgets?api-version=2026-01-01";

        Assert.Equal(uri, uri.WithQuery("$top", null));
    }

    [Fact]
    public void WithQuery_StartsOrContinuesTheQueryString()
    {
        Assert.Equal("https://example.com/widgets?$top=5", "https://example.com/widgets".WithQuery("$top", 5));
        Assert.Equal(
            "https://example.com/widgets?api-version=1&verbose=true",
            "https://example.com/widgets?api-version=1".WithQuery("verbose", true));
    }

    [Fact]
    public void WithQuery_EscapesTheValueButNotTheName()
    {
        Assert.Equal(
            "https://example.com/widgets?$filter=a%20b%2Bc%26d%3De",
            "https://example.com/widgets".WithQuery("$filter", "a b+c&d=e"));
    }

    [Theory]
    [InlineData(",", "a%20b%2Cc%26d")]
    [InlineData("\t", "a%20b%09c%26d")]
    [InlineData(" ", "a%20b%20c%26d")]
    [InlineData("|", "a%20b%7Cc%26d")]
    [InlineData(null, "a%20b&include=c%26d")]
    public void WithQuery_UsesTheDeclaredCollectionFormat(string? delimiter, string expected)
    {
        const string uri = "https://example.com/widgets";
        string[] values = ["a b", "c&d"];

        Assert.Equal($"{uri}?include={expected}", uri.WithQuery("include", values, delimiter));
        Assert.Equal($"{uri}?version=1&include={expected}", $"{uri}?version=1".WithQuery("include", values, delimiter));
        Assert.Equal($"{uri}?include=a%20b", uri.WithQuery("include", values.Take(1), delimiter));
    }

    [Theory]
    [InlineData(",")]
    [InlineData("\t")]
    [InlineData(" ")]
    [InlineData("|")]
    [InlineData(null)]
    public void WithQuery_LeavesNullAndEmptyCollectionsOff(string? delimiter)
    {
        const string uri = "https://example.com/widgets?version=1";

        Assert.Equal(uri, uri.WithQuery<string>("include", null, delimiter));
        Assert.Equal(uri, uri.WithQuery("include", Array.Empty<string>(), delimiter));
        Assert.Equal($"{uri}&include=", uri.WithQuery("include", new[] { string.Empty }, delimiter));
    }

    [Theory]
    [InlineData("de-DE")]
    [InlineData("sv-SE")]
    public void WithQuery_FormatsCollectionItemsInvariantly(string culture)
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            const string uri = "https://example.com/widgets";

            Assert.Equal($"{uri}?values=0.5%2C1234.5", uri.WithQuery("values", new[] { 0.5, 1234.5 }, ","));
            Assert.Equal($"{uri}?values=0.5&values=1234.5", uri.WithQuery("values", new[] { 0.5, 1234.5 }, null));
            Assert.Equal($"{uri}?values=true%2Cfalse", uri.WithQuery("values", new[] { true, false }, ","));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void WithQuery_KeepsScalarBytesAsBase64()
    {
        Assert.Equal("https://example.com/widgets?blockid=AQID",
            "https://example.com/widgets".WithQuery("blockid", new byte[] { 1, 2, 3 }));
    }
}
