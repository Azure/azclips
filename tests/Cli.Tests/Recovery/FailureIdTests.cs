// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using Core.Abstractions.Recovery;

namespace Cli.Tests.Recovery;

public sealed class FailureIdTests
{
    [Theory]
    [InlineData(0UL, "0000")]
    [InlineData(9UL, "0009")]
    [InlineData(10UL, "0010")]
    [InlineData(9999UL, "9999")]
    [InlineData(10000UL, "10000")]
    [InlineData(ulong.MaxValue, "18446744073709551615")]
    public void Counter_IsMinimumWidthFourWithoutTruncation(ulong sequence, string text)
    {
        var timestamp = new DateTimeOffset(2026, 9, 23, 10, 4, 59, TimeSpan.Zero);
        var value = FailureId.Format(uint.MaxValue, timestamp, sequence);
        Assert.Equal($"4294967295-2026-0923-1004-{text}", value);
        Assert.True(FailureId.TryParse(value, out var parsed));
        Assert.Equal(uint.MaxValue, parsed.TerminalSessionId);
        Assert.Equal(sequence, parsed.Sequence);
        Assert.Equal(new DateTimeOffset(2026, 9, 23, 10, 4, 0, TimeSpan.Zero), parsed.OccurredAtUtc);
    }

    [Fact]
    public void Formatter_UsesUtcAndInvariantCultureAcrossDateBoundaries()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
            var timestamp = new DateTimeOffset(2026, 1, 1, 0, 15, 0, TimeSpan.FromHours(10));
            Assert.Equal("1-2025-1231-1415-0000", FailureId.Format(1, timestamp, 0));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("48213-2026-0923-1004-000")]
    [InlineData("048213-2026-0923-1004-0000")]
    [InlineData("0-2026-0923-1004-0000")]
    [InlineData("4294967296-2026-0923-1004-0000")]
    [InlineData("48213-2026-0229-1004-0000")]
    [InlineData("48213-0000-0101-0000-0000")]
    [InlineData("48213-2026-0923-2400-0000")]
    [InlineData("48213-2026-0923-1004-00000")]
    [InlineData("48213-2026-0923-1004-18446744073709551616")]
    [InlineData("48213-2026-0923-1004-+0001")]
    [InlineData("48213-2026-0923-1004-0000 ")]
    [InlineData("abc0123456789abcdef0123456789abcd")]
    public void Parser_RejectsNoncanonicalOrOutOfRangeIds(string? value) =>
        Assert.False(FailureId.TryParse(value, out _));
}
