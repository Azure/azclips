// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Recovery;
using Core.Cli;
using Core.Cli.Parser;
using Core.Cli.Recovery;
using Moq;
using Xunit;

namespace Cli.Tests.Recovery;

[Collection("Azclips Service Provider Tests")]
public class FailureCaptureBehaviorTests
{
    [Fact]
    public async Task Save_PreservesInputAndOriginalTerminalBeforeWorkerLaunch()
    {
        var store = CreateStore();
        var command = "vm create --name \"two words\" --value \u4e2d\u6587";
        var error = new InvalidOperationException(" \r\n" + new string('x', 80_000) + "\t");
        var arguments = new[] { "vm", "create", "--name", "two words" };
        FailureRecord? sent = null;
        var entry = new FailureHistoryEntry("321-2026-0924-0900-0001", DateTimeOffset.UtcNow);

        var result = await FailureCapture.SaveAsync(arguments, command, error, 1, "vm create", null,
            () => new AgentOptions(), () => store.Object, false,
            persist: (record, size, _) =>
            {
                Assert.Equal(10, size);
                sent = record;
                return Task.FromResult(entry);
            }, getParentProcessId: () => 321);

        Assert.NotNull(sent);
        Assert.Equal(command, sent.FailedCommand);
        Assert.Equal(error.Message, sent.ErrorText);
        Assert.Equal(arguments, sent.Arguments);
        Assert.Equal(321u, sent.TerminalSessionId);
        Assert.Equal(7ul, sent.HistoryGeneration);
        Assert.Empty(sent.Id);
        Assert.Same(entry, result.Entry);
        Assert.True(result.ShowHint);
        store.Verify(s => s.SaveAsync(It.IsAny<FailureRecord>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Save_TimeoutDoesNotClaimACommit()
    {
        var store = CreateStore();
        var elapsed = Stopwatch.StartNew();
        var result = await FailureCapture.SaveAsync(["vm", "show"], "vm show", new IOException("original"), 1,
            "vm show", null, () => new AgentOptions(), () => store.Object, false,
            persist: async (_, _, ct) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
                throw new Xunit.Sdk.XunitException("The timed-out writer must not return an entry.");
            }, getParentProcessId: () => 321);

        Assert.Equal(TimeSpan.FromSeconds(2), FailureCapture.CaptureBudget);
        Assert.InRange(elapsed.Elapsed.TotalSeconds, 1.8, 2.8);
        Assert.Null(result.Entry);
        Assert.Null(result.Recovery);
        Assert.False(result.ShowHint);
        Assert.Contains("not confirmed", result.Warning);
    }

    [Fact]
    public async Task Save_UserCancellationDoesNotBecomeAStorageWarning()
    {
        var store = CreateStore();
        using var user = new CancellationTokenSource();
        var result = await FailureCapture.SaveAsync(["vm", "show"], "vm show", new IOException("original"), 1,
            "vm show", null, () => new AgentOptions(), () => store.Object, false, user.Token,
            persist: (_, _, ct) =>
            {
                user.Cancel();
                return Task.FromCanceled<FailureHistoryEntry>(ct);
            }, getParentProcessId: () => 321);

        Assert.True(result.Canceled);
        Assert.Null(result.Entry);
        Assert.Null(result.Warning);
    }

    [Fact]
    public async Task Save_HttpTimeoutIsAnOriginalFailureRatherThanUserCancellation()
    {
        var store = CreateStore();
        var result = await FailureCapture.SaveAsync(["vm", "show"], "vm show",
            new TaskCanceledException("HTTP request timeout", new TimeoutException()), 1, "vm show", null,
            () => new AgentOptions(), () => store.Object, false,
            persist: (_, _, _) => Task.FromResult(new FailureHistoryEntry("321-2026-0924-0900-0001", DateTimeOffset.UtcNow)),
            getParentProcessId: () => 321);

        Assert.NotNull(result.Entry);
        Assert.False(result.Canceled);
    }

    [Theory]
    [InlineData(OutputFormat.Table)]
    [InlineData(OutputFormat.Tsv)]
    public async Task Report_WritesOriginalHumanErrorBeforeCaptureAndBothHints(OutputFormat format)
    {
        using var stderr = new StringWriter();
        var previous = Console.Error;
        Console.SetError(stderr);
        try
        {
            var entry = new FailureHistoryEntry("321-2026-0924-0900-0001", DateTimeOffset.UtcNow);
            await Program.ReportFailureAsync(AzclipsErrorException.UsageError("original usage failure"),
                format, false, null, () =>
                {
                    Assert.Contains("original usage failure", stderr.ToString());
                    Assert.DoesNotContain("az troubleshoot", stderr.ToString());
                    return Task.FromResult(new FailureCaptureResult(entry, ShowHint: true));
                });

            var text = stderr.ToString();
            Assert.Contains($"az troubleshoot --id {entry.Id}", text);
            Assert.Contains(FailureCaptureResult.DefaultHint, text);
        }
        finally
        {
            Console.SetError(previous);
        }
    }

    [Theory]
    [InlineData(OutputFormat.Json, false)]
    [InlineData(OutputFormat.Json, true)]
    [InlineData(OutputFormat.Jsonc, false)]
    [InlineData(OutputFormat.Jsonc, true)]
    public async Task Report_KeepsOneJsonDocumentForAllErrorShapes(OutputFormat format, bool structured)
    {
        using var stderr = new StringWriter();
        using var stdout = new StringWriter();
        var previousError = Console.Error;
        var previousOutput = Console.Out;
        Console.SetError(stderr);
        Console.SetOut(stdout);
        try
        {
            const string text = "original <value> \u4e2d\u6587\r\n\"quoted\"";
            Exception error = structured ? AzclipsErrorException.UsageError(text) : new HttpRequestException(text);
            await Program.ReportFailureAsync(error, format, false, null, () =>
            {
                Assert.Empty(stderr.ToString());
                return Task.FromResult(new FailureCaptureResult(Warning: "capture was not confirmed"));
            });

            using var document = JsonDocument.Parse(stderr.ToString());
            Assert.Equal(text, document.RootElement.GetProperty("error").GetProperty("message").GetString());
            Assert.Equal("capture was not confirmed", document.RootElement.GetProperty("recoveryWarning").GetString());
            Assert.Contains("<value>", stderr.ToString());
            Assert.DoesNotContain("To troubleshoot", stderr.ToString());
            Assert.Empty(stdout.ToString());
        }
        finally
        {
            Console.SetError(previousError);
            Console.SetOut(previousOutput);
        }
    }

    [Fact]
    public void TerminalSession_IdentifiesALiveParentRatherThanTheCurrentProcess()
    {
        var parentId = TerminalSession.GetParentProcessId();
        Assert.NotEqual((uint)Environment.ProcessId, parentId);
        using var parent = Process.GetProcessById(checked((int)parentId));
        Assert.False(parent.HasExited);
    }

    private static Mock<IFailureHistoryStore> CreateStore()
    {
        var store = new Mock<IFailureHistoryStore>(MockBehavior.Strict);
        store.Setup(s => s.ReadGenerationAsync(It.IsAny<CancellationToken>())).ReturnsAsync(7ul);
        return store;
    }
}
