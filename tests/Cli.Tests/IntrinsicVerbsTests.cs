// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Cli.Help;
using Xunit;

namespace Cli.Tests;

/// <summary>
/// Tests for <see cref="IntrinsicVerbs.TryResolve"/>, the single source of truth that maps a group
/// token to a canonical intrinsic verb. The bare verb (<c>az version</c>) is always accepted; the
/// top-level-flag form (<c>az --version</c>) is accepted only for verbs that opt in via
/// <see cref="IntrinsicVerb.AllowFlagForm"/>, so help routing and dispatch treat them identically.
/// </summary>
public class IntrinsicVerbsTests
{
    [Theory]
    [InlineData("version")]
    [InlineData("--version")]
    [InlineData("VERSION")]
    [InlineData("--VERSION")]
    public void TryResolve_ShouldResolveToCanonicalVerb_ForBareAndFlagForms(string token)
    {
        var resolved = IntrinsicVerbs.TryResolve(token, out var verb);

        Assert.True(resolved);
        // Canonical casing is returned regardless of input casing, so case-sensitive lookups
        // (e.g. help-doc folders on Linux CI) stay stable.
        Assert.Equal("version", verb);
    }

    [Theory]
    [InlineData("login")]
    [InlineData("--login")]
    [InlineData("help")]
    [InlineData("--help")]
    [InlineData("--")]
    [InlineData("")]
    public void TryResolve_ShouldReturnFalse_ForNonIntrinsicTokens(string token)
    {
        Assert.False(IntrinsicVerbs.TryResolve(token, out _));
    }

    [Theory]
    [InlineData("login")]
    [InlineData("--login")]
    [InlineData("--version-ish")]
    public void TryResolve_ShouldEchoOriginalToken_OnFailure(string token)
    {
        // On a miss the out is the untouched input (not the `--`-stripped candidate), so callers can
        // report exactly what the user typed.
        Assert.False(IntrinsicVerbs.TryResolve(token, out var verb));
        Assert.Equal(token, verb);
    }

    [Fact]
    public void All_ShouldExposeMetadata_ForVersionIntrinsic()
    {
        // `version` opts into the flag form, so both `az version` and `az --version` are valid; a
        // future bare-only intrinsic would set AllowFlagForm to false and reject its `--` form.
        var version = Assert.Single(IntrinsicVerbs.All, verb => verb.Name == IntrinsicVerbs.Version);

        Assert.True(version.AllowFlagForm);
    }
}
