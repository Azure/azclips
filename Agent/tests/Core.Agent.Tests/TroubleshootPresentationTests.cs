// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.ComponentModel;
using Core.Abstractions;
using Core.Abstractions.Recovery;
using Microsoft.Azure.Azclips.Extensions.Agent.Processors.Troubleshoot;
using Xunit;
using static Core.Agent.Tests.TroubleshootTestSupport;

namespace Core.Agent.Tests;

public class TroubleshootPresentationTests
{
    [Fact]
    public void Selection_UsesPersistedSequenceNotListOrderOrTimestamp()
    {
        var a = MakeRecord(10, 1) with { OccurredAtUtc = DateTimeOffset.UtcNow };
        var b = MakeRecord(10, 2);
        var c = MakeRecord(2, 3);
        FailureRecord[] records = [a, c, b];

        Assert.Same(b, TroubleshootSelection.Select(records, null, () => 10, UnexpectedWarning));
        Assert.Same(c, TroubleshootSelection.Select(records, null, () => 99, UnexpectedWarning));
        Assert.Same(c, TroubleshootSelection.Select(records, null, () => 2, UnexpectedWarning));
        Assert.Equal([c.Id, a.Id, b.Id], TroubleshootEntry.List(records).Select(entry => entry.Id));
    }

    [Fact]
    public void Selection_ExactIdsNeverResolvePidOrFallBack()
    {
        var a = MakeRecord(sequence: 1);
        var b = MakeRecord(sequence: 2);
        uint ResolvePid() => throw new Xunit.Sdk.XunitException("An exact ID must not query the parent PID.");

        Assert.Same(a, TroubleshootSelection.Select([b, a], a.Id, ResolvePid, UnexpectedWarning));
        Assert.Equal("FailureRecordNotFound", Assert.Throws<AzclipsErrorException>(() =>
            TroubleshootSelection.Select([a, b], Id, ResolvePid, UnexpectedWarning)).Code);
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
        var older = MakeRecord(10, 3);
        var latest = MakeRecord(99, 4);
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
        var a = MakeRecord(100, 1);
        var b = MakeRecord(2, 2);
        var c = MakeRecord(2, 3);
        var d = MakeRecord(2, 4) with { OccurredAtUtc = b.OccurredAtUtc.AddMinutes(1) };
        Assert.Equal([d.Id, c.Id, b.Id, a.Id], TroubleshootEntry.List([a, b, c, d]).Select(entry => entry.Id));
    }

    [Theory]
    [InlineData(199)]
    [InlineData(200)]
    [InlineData(201)]
    public void List_LimitsOnlyTheErrorSummary(int length)
    {
        var record = MakeRecord() with { ErrorText = new string('é', length) };
        var entry = Assert.Single(TroubleshootEntry.List([record]));
        Assert.Equal(Math.Min(length, 200), entry.ErrorDetail.Length);
        Assert.Equal(record.FailedCommand, entry.Command);
        Assert.Equal(length, record.ErrorText.Length);
        Assert.Equal(record.ErrorText, TroubleshootEntry.Detail(record).ErrorDetail);
    }

    [Fact]
    public void List_UsesSingleLineWhitespaceAndDoesNotSplitUnicodeSurrogates()
    {
        var summary = TroubleshootEntry.SummarizeError("one\r\ntwo\u0085three\u2028four\u2029five\ttab");
        Assert.DoesNotContain('\r', summary);
        Assert.DoesNotContain('\n', summary);
        Assert.DoesNotContain('\u0085', summary);
        Assert.DoesNotContain('\u2028', summary);
        Assert.DoesNotContain('\u2029', summary);
        Assert.DoesNotContain('\t', summary);
        Assert.Equal(new string('x', 199), TroubleshootEntry.SummarizeError(new string('x', 199) + "\U0001F600tail"));
    }

    private static void UnexpectedWarning(string message) => throw new Xunit.Sdk.XunitException(message);
}
