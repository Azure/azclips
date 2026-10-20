// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Core.Abstractions;
using Core.Abstractions.Recovery;
using Core.Cli;
using Core.Cli.Parser;
using Core.Cli.Recovery;
using Core.Runtime.Recovery;
using Moq;
using Xunit;

namespace Cli.Tests.Recovery;

[Collection("Azclips Service Provider Tests")]
public class FailureCaptureBehaviorTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(false, "help", "no-such-group")]
    [InlineData(false, "--debug", "help", "no-such-group")]
    [InlineData(false, "vm", "--help")]
    [InlineData(false, "vm", "-?")]
    [InlineData(false, "version", "--query", "[")]
    [InlineData(false, "--output", "invalid", "--version")]
    [InlineData(false, "--output=invalid", "vm", "-?")]
    [InlineData(false, "version", "--output")]
    [InlineData(false, "troubleshoot", "--id", "missing")]
    [InlineData(false, "--completion-values")]
    [InlineData(false, "--print-probe")]
    [InlineData(true, "vm", "show")]
    [InlineData(true, "vm", "create", "--name", "help")]
    [InlineData(true, "vm", "create", "--query", "--help")]
    [InlineData(true, "--query", "version", "vm", "show")]
    public void Eligibility_ExcludesUtilitiesWithoutThrowingOnMalformedGlobalValues(bool expected, params string[] args)
    {
        Assert.Equal(expected, FailureCapture.ShouldCapture(args, new IOException("original failure"), false));
    }

    [Theory]
    [InlineData("help", "no-such-group")]
    [InlineData("version", "--query", "[")]
    [InlineData("--output", "invalid", "--version")]
    [InlineData("version", "--output")]
    public async Task Save_UtilityFailureDoesNotOpenHistoryOrReadOptions(params string[] args)
    {
        var result = await FailureCapture.SaveAsync(args, string.Join(" ", args), new IOException("utility failed"),
            1, null, null,
            () => throw new Xunit.Sdk.XunitException("Utilities must not read recovery options."),
            () => throw new Xunit.Sdk.XunitException("Utilities must not open failure history."),
            false);
        Assert.Null(result.Entry);
        Assert.Null(result.Recovery);
        Assert.Null(result.Warning);
        Assert.False(result.ShowHint);
    }

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
            () => Manual(), () => store.Object, false,
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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Save_WarnsOnlyWhenTheFailureWasStoredUnencrypted(bool unencrypted)
    {
        var store = CreateStore();
        var entry = new FailureHistoryEntry("321-2026-0924-0900-0001", DateTimeOffset.UtcNow, unencrypted);

        var result = await FailureCapture.SaveAsync(["vm", "show"], "vm show", new IOException("original failure"), 1, "vm show", null,
            () => Manual(), () => store.Object, false,
            persist: (_, _, _) => Task.FromResult(entry), getParentProcessId: () => 321);

        Assert.Same(entry, result.Entry);
        Assert.True(result.ShowHint);
        Assert.Equal(unencrypted ? FailureCaptureResult.UnencryptedWarning : null, result.Warning);
    }

    [Fact]
    public async Task Save_TimeoutDoesNotClaimACommit()
    {
        var store = CreateStore();
        var elapsed = Stopwatch.StartNew();
        var result = await FailureCapture.SaveAsync(["vm", "show"], "vm show", new IOException("original"), 1,
            "vm show", null, () => Manual(), () => store.Object, false,
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
            "vm show", null, () => Manual(), () => store.Object, false, user.Token,
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
            () => Manual(), () => store.Object, false,
            persist: (_, _, _) => Task.FromResult(new FailureHistoryEntry("321-2026-0924-0900-0001", DateTimeOffset.UtcNow)),
            getParentProcessId: () => 321);

        Assert.NotNull(result.Entry);
        Assert.False(result.Canceled);
    }

    [Fact]
    public async Task Save_OptionsBarrierCannotAdoptThePostResetGeneration()
    {
        using var scope = new FailureHistoryTestScope();
        var store = scope.CreateStore();
        _ = scope.Configuration.GetConfiguration<AgentOptions>();
        var optionsRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reads = 0;
        var capture = Task.Run(() => FailureCapture.SaveAsync(["vm", "show"], "vm show",
            new IOException("original failure"), 1, "vm show", null, () =>
            {
                var options = scope.Configuration.GetConfiguration<AgentOptions>();
                // The first read is the off gate; block the read that follows generation registration.
                if (++reads == 1)
                    return options;
                optionsRead.SetResult();
                resume.Task.GetAwaiter().GetResult();
                return options;
            }, () => store, false, getParentProcessId: () => 321));

        try
        {
            await optionsRead.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await scope.Configuration.SetConfigurationAsync("agent.recovery", "off");
            await scope.Configuration.SetConfigurationAsync("agent.recovery", "manual");
        }
        finally
        {
            resume.TrySetResult();
            await capture;
        }

        var stale = await capture;
        Assert.Null(stale.Entry);
        Assert.Null(stale.Recovery);
        Assert.False(stale.ShowHint);
        Assert.Contains("failure_history_stale_generation", stale.Warning);
        Assert.Empty(await store.ReadIndexAsync());
        var fresh = await FailureCapture.SaveAsync(["vm", "show"], "vm show", new IOException("new failure"),
            1, "vm show", null, () => scope.Configuration.GetConfiguration<AgentOptions>(), () => store, false,
            getParentProcessId: () => 321);
        Assert.NotNull(fresh.Entry);
        Assert.True(fresh.ShowHint);
        Assert.Equal(fresh.Entry, Assert.Single(await store.ReadIndexAsync()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("off")]
    public async Task Save_OffNeverTouchesHistoryStoreOrDirectory(string? trigger)
    {
        using var scope = new FailureHistoryTestScope(trigger: trigger);
        var optionReads = 0;
        var result = await FailureCapture.SaveAsync(["vm", "show"], "vm show", new IOException("original failure"),
            1, "vm show", null,
            () => { optionReads++; return scope.Configuration.GetConfiguration<AgentOptions>(); },
            () => throw new InvalidOperationException("Off must not resolve the history store."), false,
            persist: (_, _, _) => throw new InvalidOperationException("Off must not launch a writer."),
            getParentProcessId: () => throw new InvalidOperationException("Off must not resolve a terminal."));
        Assert.Null(result.Entry);
        Assert.Null(result.Warning);
        Assert.False(result.ShowHint);
        Assert.Equal(1, optionReads);
        Assert.False(Directory.Exists(scope.DirectoryPath));
    }

    [Fact]
    public void RecoveryDiagnostic_WritesBothHintsAndNeverTheOriginalError()
    {
        using var stderr = new StringWriter();
        var previous = Console.Error;
        Console.SetError(stderr);
        try
        {
            var entry = new FailureHistoryEntry("321-2026-0924-0900-0001", DateTimeOffset.UtcNow);
            Program.EmitRecoveryDiagnostic(new FailureCaptureResult(entry, ShowHint: true));

            var text = stderr.ToString();
            Assert.Contains($"az troubleshoot --id {entry.Id}", text);
            Assert.Contains(FailureCaptureResult.DefaultHint, text);
            Assert.DoesNotContain("ERROR", text);
        }
        finally
        {
            Console.SetError(previous);
        }
    }

    [Fact]
    public void RecoveryDiagnostic_WritesOnlyTheWarningWhenCaptureIsUnconfirmed()
    {
        using var stderr = new StringWriter();
        using var stdout = new StringWriter();
        var previousError = Console.Error;
        var previousOutput = Console.Out;
        Console.SetError(stderr);
        Console.SetOut(stdout);
        try
        {
            Program.EmitRecoveryDiagnostic(new FailureCaptureResult(Warning: "capture was not confirmed"));

            var output = stderr.ToString();
            Assert.Contains("capture was not confirmed", output);
            Assert.DoesNotContain("To troubleshoot", output);
            Assert.DoesNotContain("ERROR", output);
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

    private static AgentOptions Manual() => new() { Recovery = new() { Trigger = "manual" } };

    private static Mock<IFailureHistoryStore> CreateStore()
    {
        var store = new Mock<IFailureHistoryStore>(MockBehavior.Strict);
        store.Setup(s => s.BeginCaptureAsync(It.IsAny<CancellationToken>())).ReturnsAsync((ulong?)7);
        return store;
    }
}
