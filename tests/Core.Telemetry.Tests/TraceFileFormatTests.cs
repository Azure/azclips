// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Telemetry.Tests;

public class TraceFileFormatTests
{
    [Fact]
    public void Parse_ReturnsEntriesFromEveryBatch()
    {
        var content = """

            --- Batch at 2024-01-01T00:00:00.0000000Z ---
            [ { "OperationName": "first" } ]

            --- Batch at 2024-01-01T00:00:01.0000000Z ---
            [ { "OperationName": "second" }, { "OperationName": "third" } ]
            """;

        var entries = TraceFileFormat.Parse(content);

        Assert.Equal(
            new[] { "first", "second", "third" },
            entries.Select(e => e.OperationName));
    }

    [Fact]
    public void Parse_SkipsMalformedBatch_AndKeepsSiblings()
    {
        var content = """

            --- Batch at 2024-01-01T00:00:00.0000000Z ---
            [ { "OperationName": "good" } ]

            --- Batch at 2024-01-01T00:00:01.0000000Z ---
            [ { "OperationName": not-json } ]
            """;

        var entries = TraceFileFormat.Parse(content);

        Assert.Equal("good", Assert.Single(entries).OperationName);
    }

    [Fact]
    public void Parse_ReturnsEmpty_WhenContentHasNoJson()
    {
        var entries = TraceFileFormat.Parse("--- Batch at 2024-01-01T00:00:00.0000000Z ---");

        Assert.Empty(entries);
    }

    [Fact]
    public void Parse_ReturnsEmpty_WhenContentIsEmpty()
    {
        Assert.Empty(TraceFileFormat.Parse(string.Empty));
    }

    [Fact]
    public void CompleteParser_RejectsMalformedSiblingInsteadOfSilentlyDroppingIt()
    {
        var valid = TraceFileFormat.FormatBatch([new { OperationName = "root" }], DateTime.UtcNow);
        var content = valid + "\n--- Batch at 2024-01-01T00:00:00.0000000Z ---\n[{";

        Assert.False(TraceFileFormat.TryParseComplete(content, out _));
        Assert.Single(TraceFileFormat.Parse(content));
    }

    [Fact]
    public void CompleteParser_HandlesHeaderTextInsideJsonString()
    {
        var content = TraceFileFormat.FormatBatch(
            [new { OperationName = "contains --- Batch at text" }], DateTime.UtcNow);

        Assert.True(TraceFileFormat.TryParseComplete(content, out var entries));
        Assert.Equal("contains --- Batch at text", Assert.Single(entries).OperationName);
    }

    [Fact]
    public void Parse_SkipsBatch_WhenBracketsAreOutOfOrder()
    {
        var entries = TraceFileFormat.Parse("--- Batch at x ---\n] [");

        Assert.Empty(entries);
    }

    [Fact]
    public void FormatBatch_RoundTripsThroughParse()
    {
        var traces = new List<object>
        {
            new
            {
                TraceId = "0123456789abcdef0123456789abcdef",
                OperationName = "round-trip",
                Duration = 12.5,
                TenantId = "tenant-1",
                DataBoundary = "EU",
            },
        };

        var content = TraceFileFormat.FormatBatch(traces, new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var entries = TraceFileFormat.Parse(content);

        var entry = Assert.Single(entries);
        Assert.Equal("0123456789abcdef0123456789abcdef", entry.TraceId);
        Assert.Equal("round-trip", entry.OperationName);
        Assert.Equal(12.5, entry.Duration);
        Assert.Equal("tenant-1", entry.TenantId);
        Assert.Equal("EU", entry.DataBoundary);
    }

    [Fact]
    public void FormatBatch_AppendsCleanly_SoTwoBatchesBothParse()
    {
        var first = TraceFileFormat.FormatBatch(
            new List<object> { new { OperationName = "one" } },
            new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var second = TraceFileFormat.FormatBatch(
            new List<object> { new { OperationName = "two" } },
            new DateTime(2024, 1, 1, 0, 0, 1, DateTimeKind.Utc));

        var entries = TraceFileFormat.Parse(first + second);

        Assert.Equal(new[] { "one", "two" }, entries.Select(e => e.OperationName));
    }
}
