// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Recovery;

namespace Cli.Tests.Recovery;

public sealed class FailureIdTests
{
    [Fact]
    public void Create_IsThePidAndEightLowercaseHexDigits()
    {
        var value = FailureId.Create(uint.MaxValue);

        Assert.Matches("^4294967295-[0-9a-f]{8}$", value);
        Assert.True(FailureId.TryParse(value, out var parsed));
        Assert.Equal(uint.MaxValue, parsed.TerminalSessionId);
        Assert.Equal(value, parsed.ToString());
    }

    [Fact]
    public void Create_IsRandom() =>
        Assert.Equal(20, Enumerable.Range(0, 20).Select(_ => FailureId.Create(42)).Distinct().Count());

    [Fact]
    public void Create_RejectsAZeroPid() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => FailureId.Create(0));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("48213")]
    [InlineData("48213-cfd9ddb")]
    [InlineData("48213-cfd9ddb45")]
    [InlineData("48213-CFD9DDB4")]
    [InlineData("48213-cfd9ddbg")]
    [InlineData("048213-cfd9ddb4")]
    [InlineData("0-cfd9ddb4")]
    [InlineData("4294967296-cfd9ddb4")]
    [InlineData("+48213-cfd9ddb4")]
    [InlineData("48213-cfd9ddb4 ")]
    [InlineData("48213-2026-0923-1004-0000")]
    public void Parser_RejectsNoncanonicalIds(string? value) =>
        Assert.False(FailureId.TryParse(value, out _));
}
