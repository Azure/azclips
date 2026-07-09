// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Cli.Help;
using Xunit;

namespace Cli.Tests;

/// <summary>
/// Tests for <see cref="IntrinsicVerbs.TryResolve"/>, the single source of truth that maps a group
/// token to a canonical intrinsic verb. It must accept both the bare verb (<c>az version</c>) and the
/// top-level-flag form (<c>az --version</c>) so help routing treats them identically.
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
}
