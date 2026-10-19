// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Xunit;

namespace Cli.Tests;

public class CommandExclusionListTests
{
    [Theory]
    [InlineData(null, new string[0])]
    [InlineData("", new string[0])]
    [InlineData("  ", new string[0])]
    [InlineData("login", new[] { "login" })]
    [InlineData("login, profile show ;vm", new[] { "login", "profile show", "vm" })]
    [InlineData(",,; ;login,", new[] { "login" })]
    public void Split_SplitsOnCommaAndSemicolon(string? value, string[] expected)
    {
        Assert.Equal(expected, CommandExclusionList.Split(value));
    }

    [Theory]
    [InlineData("az  Profile   SHOW", "profile show")]
    [InlineData("profile show", "profile show")]
    [InlineData("az az login", "az login")]
    [InlineData("az", "")]
    [InlineData("   ", "")]
    public void Normalize_CollapsesWhitespaceDropsLeadingAzAndLowercases(string entry, string expected)
    {
        Assert.Equal(expected, CommandExclusionList.Normalize(entry));
    }

    [Fact]
    public void Add_KeepsOriginalSpellingOrderAndFirstWins()
    {
        Assert.Equal("Login,profile show", CommandExclusionList.Add("Login", "profile show;az LOGIN"));
        Assert.Equal("az Profile Show", CommandExclusionList.Add("az Profile Show", "profile   show"));
        Assert.Equal("login", CommandExclusionList.Add("", "login"));
        Assert.Equal("login,vm", CommandExclusionList.Add("login", "vm,login"));
    }

    [Fact]
    public void Remove_MatchesByNormalizedForm()
    {
        Assert.Equal("vm", CommandExclusionList.Remove("az Login,vm", "LOGIN"));
        Assert.Equal("", CommandExclusionList.Remove("login", "login"));
        Assert.Equal("login", CommandExclusionList.Remove("login", "missing"));
        Assert.Equal("c", CommandExclusionList.Remove("a;b,c", "a,b"));
    }

    [Fact]
    public void Join_UsesComma()
    {
        Assert.Equal("a,b", CommandExclusionList.Join(["a", "b"]));
    }

    [Theory]
    [InlineData("profile", new[] { "profile", "show" }, true, true)]
    [InlineData("profile show", new[] { "profile", "show" }, true, true)]
    [InlineData("az PROFILE  show", new[] { "profile", "show" }, true, true)]
    [InlineData("profile show", new[] { "profile", "list" }, true, false)]
    [InlineData("profile show extra", new[] { "profile", "show" }, true, false)]
    [InlineData("config", new[] { "configuration", "show" }, true, false)]
    [InlineData("config", new[] { "config", "set" }, true, true)]
    [InlineData("", new[] { "login" }, true, false)]
    [InlineData("az", new[] { "login" }, true, false)]
    [InlineData("az, ;", new[] { "login" }, true, false)]
    [InlineData("vm,login", new[] { "login" }, true, true)]
    [InlineData("login", new[] { "login" }, false, true)]
    [InlineData("profile show", new[] { "profile", "show" }, false, false)]
    [InlineData("profile", new[] { "profile", "show" }, false, true)]
    [InlineData("config nonexistent", new[] { "config", "nonexistent" }, false, false)]
    [InlineData("config", new[] { "config", "nonexistent" }, false, true)]
    [InlineData("config set", new[] { "config", "set" }, true, true)]
    public void IsExcluded_UsesTokenPrefixMatching(string list, string[] tokens, bool resolved, bool expected)
    {
        Assert.Equal(expected, CommandExclusionList.IsExcluded(list, tokens, resolved));
    }

    [Fact]
    public void IsExcluded_EmptyCommand_NeverMatches()
    {
        Assert.False(CommandExclusionList.IsExcluded("login", [], true));
    }
}
