// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Recovery;
using Microsoft.Azure.Azclips.Extensions.Agent.Processors.Troubleshoot;
using Xunit;
using static Core.Agent.Tests.TroubleshootTestSupport;

namespace Core.Agent.Tests;

[Collection("Troubleshoot")]
public class TroubleshootProcessorTests
{
    public static TheoryData<string?, string?> InvalidArguments => new()
    {
        { @"..\history", null },
        { "42-2026-0924-0000", null },
        { "abcdef0123456789abcdef0123456789", null },
    };

    [Theory]
    [MemberData(nameof(InvalidArguments))]
    public async Task InvalidArguments_FailBeforeHistoryOrAgent(string? id, string? style)
    {
        var history = new FakeHistory();
        var error = await Assert.ThrowsAsync<UsageErrorException>(() =>
            RunAsync(new TroubleshootAnalyzeProcessor { Id = id, Style = style }, history));
        Assert.Equal(2, error.ExitCode);
        Assert.Equal(0, history.Reads);
    }

    [Fact]
    public async Task List_IsReadOnlyAndUsesOnlyTheThreeOriginalFields()
    {
        var record = MakeRecord() with { ErrorText = "original\r\nfailure é \"quoted\"" + new string('x', 220) };
        var history = new FakeHistory(record);
        var written = await RunAsync(new TroubleshootListProcessor(), history);

        var entry = Assert.Single(Assert.IsType<TroubleshootEntry[]>(Assert.Single(written)));
        Assert.Equal(["Id", "Command", "ErrorDetail"], typeof(TroubleshootEntry).GetProperties().Select(p => p.Name));
        Assert.Equal(record.Id, entry.Id);
        Assert.Equal(record.FailedCommand, entry.Command);
        Assert.Equal(TroubleshootEntry.SummarizeError(record.ErrorText), entry.ErrorDetail);
        Assert.Equal(1, history.Reads);
    }

    [Fact]
    public async Task Show_UsesFullOriginalContent()
    {
        var record = MakeRecord() with { ErrorText = "first\nsecond\r\n" + new string('é', 400) };
        var history = new FakeHistory(record);
        var written = await RunAsync(new TroubleshootShowProcessor { Id = record.Id }, history);

        var entry = Assert.IsType<TroubleshootEntry>(Assert.Single(written));
        Assert.Equal(record.ErrorText, entry.ErrorDetail);
        Assert.Equal(record.FailedCommand, entry.Command);
        Assert.Equal(1, history.Reads);
    }

    [Fact]
    public async Task LocalViews_DistinguishEmptyAndMissingRecords()
    {
        var written = await RunAsync(new TroubleshootListProcessor(), new FakeHistory());
        Assert.Empty(Assert.IsType<TroubleshootEntry[]>(Assert.Single(written)));

        var missing = await Assert.ThrowsAsync<AzclipsErrorException>(() =>
            RunAsync(new TroubleshootShowProcessor { Id = Id }, new FakeHistory()));
        Assert.Equal("FailureRecordNotFound", missing.Code);
        Assert.Equal(1, missing.ExitCode);
    }

    [Theory]
    [InlineData("failure_history_incompatible_draft")]
    [InlineData("failure_history_key_unavailable")]
    [InlineData("failure_history_corrupt")]
    [InlineData("failure_history_clear_pending")]
    public async Task LocalViews_ReportHistoryErrorsWithoutCleanupOrFallback(string code)
    {
        foreach (var processor in new Microsoft.Azure.Azclips.Extensions.Agent.Processors.AgentProcessorBase[]
            { new TroubleshootListProcessor(), new TroubleshootShowProcessor { Id = Id } })
        {
            var history = new FakeHistory(new FailureHistoryException(code, "Local history cannot be read."));
            var error = await Assert.ThrowsAsync<AzclipsErrorException>(() => RunAsync(processor, history));
            Assert.Equal(code, error.Code);
            Assert.Equal(1, error.ExitCode);
            Assert.Equal(1, history.Reads);
        }
    }

    [Fact]
    public async Task UnavailableKey_WithoutId_UsesTheNewestPlainRecord()
    {
        var plain = MakeRecord(sequence: 3);
        var unavailable = new FailureHistoryException("failure_history_key_unavailable", "Locked.");
        Assert.Same(plain, Assert.Single(await TroubleshootAnalyzeProcessor.ReadRecordsAsync(
            new FakeHistory(unavailable, plain), null, CancellationToken.None)));
        Assert.Same(unavailable, await Assert.ThrowsAsync<FailureHistoryException>(() =>
            TroubleshootAnalyzeProcessor.ReadRecordsAsync(new FakeHistory(unavailable, plain), plain.Id, CancellationToken.None)));
        Assert.Same(unavailable, await Assert.ThrowsAsync<FailureHistoryException>(() =>
            TroubleshootAnalyzeProcessor.ReadRecordsAsync(new FakeHistory(unavailable), null, CancellationToken.None)));
    }

    [Fact]
    public async Task DisabledAnalysis_DoesNotReadHistory()
    {
        var history = new FakeHistory();
        var error = await Assert.ThrowsAsync<AzclipsErrorException>(() => RunAsync(new TroubleshootAnalyzeProcessor(), history,
            new FakeConfiguration(new AgentOptions { Recovery = new() { Trigger = "off" } })));
        Assert.Equal("AgentDisabled", error.Code);
        Assert.Contains("agent.recovery.trigger", error.Message);
        Assert.Equal(0, history.Reads);
    }

    [Theory]
    [InlineData(null, "NoFailureHistory")]
    [InlineData(Id, "FailureRecordNotFound")]
    public async Task Analysis_EmptyOrEvictedSelectionCannotStartAgent(string? id, string expectedCode)
    {
        var history = new FakeHistory();
        var error = await Assert.ThrowsAsync<AzclipsErrorException>(() =>
            RunAsync(new TroubleshootAnalyzeProcessor { Id = id }, history, new FakeConfiguration(Manual())));
        Assert.Equal(expectedCode, error.Code);
        Assert.Equal(1, error.ExitCode);
        Assert.Equal(1, history.Reads);
    }
}
