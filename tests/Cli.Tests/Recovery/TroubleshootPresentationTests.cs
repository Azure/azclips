// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.ComponentModel;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Recovery;
using Core.Cli.Parser;
using Core.Cli.Recovery;

namespace Cli.Tests.Recovery;

public class TroubleshootPresentationTests
{
    [Fact]
    public void Selection_UsesPersistedSequenceNotListOrderOrTimestamp()
    {
        var a = TroubleshootCommandTests.MakeRecord(10, 1) with { OccurredAtUtc = DateTimeOffset.UtcNow };
        var b = TroubleshootCommandTests.MakeRecord(10, 2);
        var c = TroubleshootCommandTests.MakeRecord(2, 3);
        FailureRecord[] records = [a, c, b];

        Assert.Same(b, TroubleshootSelection.Select(records, null, () => 10, UnexpectedWarning));
        Assert.Same(c, TroubleshootSelection.Select(records, null, () => 99, UnexpectedWarning));
        Assert.Same(c, TroubleshootSelection.Select(records, null, () => 2, UnexpectedWarning));
        Assert.Equal([c.Id, a.Id, b.Id], TroubleshootOutput.List(records).Select(entry => entry.Id));
    }

    [Fact]
    public void Selection_ExactIdsNeverResolvePidOrFallBack()
    {
        var a = TroubleshootCommandTests.MakeRecord(sequence: 1);
        var b = TroubleshootCommandTests.MakeRecord(sequence: 2);
        uint ResolvePid() => throw new Xunit.Sdk.XunitException("An exact ID must not query the parent PID.");

        Assert.Same(a, TroubleshootSelection.Select([b, a], a.Id, ResolvePid, UnexpectedWarning));
        Assert.Equal("FailureRecordNotFound", Assert.Throws<AzclipsErrorException>(() =>
            TroubleshootSelection.Select([a, b], TroubleshootCommandTests.Id, ResolvePid, UnexpectedWarning)).Code);
        Assert.Equal("FailureRecordNotFound", Assert.Throws<AzclipsErrorException>(() =>
            TroubleshootSelection.Select([], a.Id, ResolvePid, UnexpectedWarning)).Code);
        Assert.Equal("NoFailureHistory", Assert.Throws<AzclipsErrorException>(() =>
            TroubleshootSelection.Select([], null, ResolvePid, UnexpectedWarning)).Code);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Selection_PidFailureWarnsAndUsesGlobalLatestWithoutInventingPid(bool throws)
    {
        var older = TroubleshootCommandTests.MakeRecord(10, 3);
        var latest = TroubleshootCommandTests.MakeRecord(99, 4);
        var warnings = new List<string>();
        var selected = TroubleshootSelection.Select([older, latest], null,
            () => throws ? throw new Win32Exception("sensitive diagnostic") : 0u, warnings.Add);
        Assert.Same(latest, selected);
        var warning = Assert.Single(warnings);
        Assert.Contains("Unable to resolve", warning);
        Assert.Contains("across all terminals", warning);
        Assert.DoesNotContain("sensitive diagnostic", warning);
    }

    [Fact]
    public void List_SortsNumericPidThenUtcThenCounterDescending()
    {
        var a = TroubleshootCommandTests.MakeRecord(100, 1);
        var b = TroubleshootCommandTests.MakeRecord(2, 2);
        var c = TroubleshootCommandTests.MakeRecord(2, 3);
        var d = TroubleshootCommandTests.MakeRecord(2, 4) with { OccurredAtUtc = b.OccurredAtUtc.AddMinutes(1) };
        Assert.Equal([d.Id, c.Id, b.Id, a.Id], TroubleshootOutput.List([a, b, c, d]).Select(entry => entry.Id));
    }

    [Theory]
    [InlineData(199)]
    [InlineData(200)]
    [InlineData(201)]
    public void List_LimitsOnlyTheErrorSummary(int length)
    {
        var record = TroubleshootCommandTests.MakeRecord() with { ErrorText = new string('é', length) };
        var entry = Assert.Single(TroubleshootOutput.List([record]));
        Assert.Equal(Math.Min(length, 200), entry.ErrorDetail.Length);
        Assert.Equal(record.FailedCommand, entry.Command);
        Assert.Equal(length, record.ErrorText.Length);
        Assert.Equal(record.ErrorText, TroubleshootOutput.Detail(record).ErrorDetail);
    }

    [Fact]
    public void List_UsesSingleLineWhitespaceAndDoesNotSplitUnicodeSurrogates()
    {
        var summary = TroubleshootOutput.SummarizeError("one\r\ntwo\u0085three\u2028four\u2029five\ttab");
        Assert.DoesNotContain('\r', summary);
        Assert.DoesNotContain('\n', summary);
        Assert.DoesNotContain('\u0085', summary);
        Assert.DoesNotContain('\u2028', summary);
        Assert.DoesNotContain('\u2029', summary);
        Assert.DoesNotContain('\t', summary);
        Assert.Equal(new string('x', 199), TroubleshootOutput.SummarizeError(new string('x', 199) + "😀tail"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(1)]
    [InlineData(12)]
    [InlineData(80)]
    public void Show_TablePreservesCompleteMultilineTextEvenWhenNarrow(int? width)
    {
        var record = TroubleshootCommandTests.MakeRecord() with
        {
            FailedCommand = "command\n" + new string('c', 400) + " \"quoted\" 雪",
            ErrorText = "first\r\nsecond\n" + new string('é', 4_000) + "\r\nlast",
        };
        var output = TroubleshootOutput.FormatDetail(record, OutputFormat.Table, OutputFormat.Table, false, null, width);
        Assert.Contains(record.Id, output);
        Assert.Contains(record.FailedCommand, output);
        Assert.Contains(record.ErrorText, output);
        Assert.DoesNotContain("...", output);
        Assert.DoesNotContain("arguments", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Show_TableQueryRetainsFullMultilineText()
    {
        var record = TroubleshootCommandTests.MakeRecord() with { ErrorText = "one\n" + new string('é', 400) };
        Assert.Equal(record.ErrorText, TroubleshootOutput.FormatDetail(
            record, OutputFormat.Table, OutputFormat.Table, false, "errorDetail", 1));
        var output = TroubleshootOutput.FormatDetail(record, OutputFormat.Json, OutputFormat.Json, false, null);
        using var json = JsonDocument.Parse(output);
        Assert.Equal(["id", "command", "errorDetail"], json.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.Equal(record.ErrorText, json.RootElement.GetProperty("errorDetail").GetString());
        Assert.Contains("é", output);
    }

    private static void UnexpectedWarning(string message) => throw new Xunit.Sdk.XunitException(message);
}
