// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
using static Core.Telemetry.Tests.TelemetryLayerTestEnvironment;

namespace Core.Telemetry.Tests;

public class TelemetryLayerFileTests
{
    [Fact]
    public async Task WriterRunsSuppressed_WithoutInheritingTheStoppedActivity()
    {
        var observed = new TaskCompletionSource<(Activity? Current, bool Suppressed)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        LayerTelemetryScope? scope = null;
        var fileSystem = new TelemetryLayerTestFileSystem
        {
            Wrap = stream =>
            {
                if (!observed.Task.IsCompleted)
                {
                    var current = Activity.Current;
                    using var probe = scope!.Telemetry.StartActivity("Test.Other");
                    observed.TrySetResult((current, probe is null));
                }
                return stream;
            },
        };
        await using var test = new TelemetryLayerTestEnvironment(clock => new TelemetryLayerScopeOptions
        {
            TimeProvider = clock, FileSystem = fileSystem, Diagnostic = _ => { },
        });
        using var caller = new Activity("caller").Start();
        scope = test.Create();
        using (scope.Telemetry.StartIndependentRootActivity("Test.Child")) { }
        var context = await observed.Task.WaitAsync(TestTimeout);
        Assert.Null(context.Current);
        Assert.True(context.Suppressed);
        Assert.Same(caller, Activity.Current);
        Assert.Equal(TelemetryLayerCompletion.Published, await scope.CompleteAsync().WaitAsync(TestTimeout));
    }

    [Fact]
    public async Task DisposingChild_AllowsItsBusinessCallerToReachCompletionWhileIoIsBlocked()
    {
        using var io = new TelemetryLayerBarrier();
        var disposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fileSystem = new TelemetryLayerTestFileSystem
        {
            Wrap = stream => new TelemetryLayerFaultStream(stream)
            {
                WriteAction = (inner, bytes, offset, count) =>
                {
                    io.Hit();
                    inner.Write(bytes, offset, count);
                },
            },
        };
        await using var test = new TelemetryLayerTestEnvironment(clock => new TelemetryLayerScopeOptions
        {
            TimeProvider = clock, FileSystem = fileSystem, Diagnostic = _ => { },
        });
        var scope = test.Create();
        TelemetryLayerCompletion? result = null;
        var business = Task.Run(async () =>
        {
            using (scope.Telemetry.StartIndependentRootActivity("Test.Child"))
            {
            }
            disposed.TrySetResult();
            result = await scope.CompleteAsync();
            return 71;
        });
        try
        {
            await io.Entered.Task.WaitAsync(TestTimeout);
            await Task.WhenAny(disposed.Task, Task.Delay(TimeSpan.FromSeconds(1)));
            Assert.True(disposed.Task.IsCompleted,
                $"Activity.Dispose blocked its business caller before completion admission; state={scope.State}.");
            Assert.False(scope.FileSession.HasRecords);
            Assert.False(scope.FileSession.IsIdle);
            await scope.WaitForStateAsync(TelemetryLayerState.Fenced).WaitAsync(TestTimeout);
            test.Clock.Advance(LayerTelemetryScope.FileCompletionBudget);
            Assert.Equal(71, await business.WaitAsync(TestTimeout));
            Assert.Equal(TelemetryLayerCompletion.NotPublished, result);
            Assert.False(File.Exists(scope.FileSession.PublishedPath));
        }
        finally
        {
            io.Release();
            await business.WaitAsync(TestTimeout);
            await scope.ProviderDisposalCompletion.WaitAsync(TestTimeout);
        }
        Assert.False(File.Exists(scope.FileSession.PublishedPath));
        Assert.Equal(0, fileSystem.Moves);
    }

    [Fact]
    public async Task AdmittedBatches_HaveOneWriter_AndPublishOnlyAfterAllRealWrites()
    {
        using var firstWrite = new TelemetryLayerBarrier();
        var writes = 0;
        var active = 0;
        var maximum = 0;
        var concurrencyGate = new object();
        var fileSystem = new TelemetryLayerTestFileSystem
        {
            Wrap = stream => new TelemetryLayerFaultStream(stream)
            {
                WriteAction = (inner, bytes, offset, count) =>
                {
                    var concurrency = Interlocked.Increment(ref active);
                    lock (concurrencyGate)
                        maximum = Math.Max(maximum, concurrency);
                    try
                    {
                        if (Interlocked.Increment(ref writes) == 1)
                            firstWrite.Hit();
                        inner.Write(bytes, offset, count);
                    }
                    finally
                    {
                        Interlocked.Decrement(ref active);
                    }
                },
            },
        };
        await using var test = new TelemetryLayerTestEnvironment(clock => new TelemetryLayerScopeOptions
        {
            TimeProvider = clock, FileSystem = fileSystem, Diagnostic = _ => { },
        });
        var scope = test.Create();
        using var rootScope = scope.Telemetry.StartIndependentRootActivity("Test.Root", activate: false);
        var root = rootScope!.Activity;
        var producers = Task.Run(() =>
        {
            for (var sequence = 0; sequence < 3; sequence++)
            {
                using var child = scope.Telemetry.StartActivityWithParent("Test.Child", root.Context);
                child!.Activity.SetTag("properties.testlayer.sequence", sequence);
            }
        });
        try
        {
            await firstWrite.Entered.Task.WaitAsync(TestTimeout);
            await Task.WhenAny(producers, Task.Delay(TimeSpan.FromSeconds(1)));
            Assert.True(producers.IsCompleted, "A queued batch blocked the activity producer.");
            Assert.Equal(1, fileSystem.Opens);
            Assert.Equal(1, writes);
            Assert.False(scope.FileSession.HasRecords);
            Assert.False(scope.FileSession.IsIdle);
            var completion = scope.CompleteAsync(root);
            await scope.WaitForStateAsync(TelemetryLayerState.Fenced).WaitAsync(TestTimeout);
            test.Clock.Advance(TimeSpan.FromSeconds(1));
            Assert.Equal(TelemetryLayerState.Fenced, scope.State);
            Assert.False(completion.IsCompleted);
            Assert.False(scope.FileSession.Published);
            firstWrite.Release();
            Assert.Equal(TelemetryLayerCompletion.Published, await completion.WaitAsync(TestTimeout));
            Assert.True(scope.FileSession.IsIdle);
            Assert.Equal(1, maximum);
            Assert.Equal(4, writes);
            var entries = test.Read(scope);
            Assert.Equal([0, 1, 2],
                entries.Take(3).Select(entry => Tag(entry, "properties.testlayer.sequence").GetInt32()));
            Assert.Equal("Test.Root", entries[^1].OperationName);
        }
        finally
        {
            firstWrite.Release();
            await producers.WaitAsync(TestTimeout);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PartialWrite_IsRolledBackBeforeRetryingTheSameCapturedBytes(bool existingBatch)
    {
        var writes = 0;
        var failedAttempt = existingBatch ? 2 : 1;
        long originalLength = 0;
        byte[]? original = null;
        var fileSystem = new TelemetryLayerTestFileSystem
        {
            Wrap = stream => new TelemetryLayerFaultStream(stream)
            {
                WriteAction = (inner, bytes, offset, count) =>
                {
                    var attempt = Interlocked.Increment(ref writes);
                    if (attempt == failedAttempt)
                    {
                        original = bytes.ToArray();
                        originalLength = inner.Length;
                        inner.Write(bytes, offset, 17);
                        inner.Flush();
                        throw new IOException("Injected partial write");
                    }
                    if (attempt == failedAttempt + 1)
                    {
                        Assert.Equal(originalLength, inner.Length);
                        Assert.Equal(original, bytes);
                    }
                    inner.Write(bytes, offset, count);
                },
            },
        };
        await using var test = new TelemetryLayerTestEnvironment(clock => new TelemetryLayerScopeOptions
        {
            TimeProvider = clock, FileSystem = fileSystem, Diagnostic = _ => { },
        });
        var scope = test.Create();
        using var rootScope = scope.Telemetry.StartIndependentRootActivity("Test.Root", activate: false);
        var root = rootScope?.Activity;
        if (existingBatch)
        {
            using var seedScope = scope.Telemetry.StartActivityWithParent("Test.Other", root!.Context);
            Assert.NotNull(seedScope?.Activity);
        }
        using var childScope = scope.Telemetry.StartActivityWithParent("Test.Child", root!.Context)!;
        var child = childScope.Activity;
        var end = Task.Run(child.Stop);
        await test.Clock.WaitForTimerAsync(TimeSpan.FromMilliseconds(50), TestTimeout);
        test.Clock.Advance(TimeSpan.FromMilliseconds(50));
        await end.WaitAsync(TestTimeout);
        childScope.Dispose();
        await scope.FileSession.WaitForIdleAsync().WaitAsync(TestTimeout);
        Assert.False(scope.FileSession.Failed);
        Assert.Equal(TelemetryLayerCompletion.Published, await scope.CompleteAsync(root).WaitAsync(TestTimeout));
        Assert.Equal(existingBatch ? 4 : 3, writes);
        Assert.Equal(existingBatch ? 3 : 2, test.Read(scope).Count);
        Assert.Equal(existingBatch, originalLength > 0);
        Assert.Equal(1, fileSystem.Moves);
    }

    [Fact]
    public async Task PersistentPartialWriteFailure_IsSticky_AndNeverUsesSuccessfulRootAsFallback()
    {
        var writes = 0;
        var fileSystem = new TelemetryLayerTestFileSystem
        {
            Wrap = stream => new TelemetryLayerFaultStream(stream)
            {
                WriteAction = (inner, bytes, offset, count) =>
                {
                    Interlocked.Increment(ref writes);
                    Assert.Equal(0, inner.Length);
                    inner.Write(bytes, offset, 13);
                    throw new IOException("Injected repeated partial write");
                },
            },
        };
        await using var test = new TelemetryLayerTestEnvironment(clock => new TelemetryLayerScopeOptions
        {
            TimeProvider = clock, FileSystem = fileSystem, Diagnostic = _ => { },
        });
        var scope = test.Create();
        using var rootScope = scope.Telemetry.StartIndependentRootActivity("Test.Root", activate: false);
        var root = rootScope?.Activity;
        using var childScope = scope.Telemetry.StartActivityWithParent("Test.Child", root!.Context)!;
        var child = childScope.Activity;
        var end = Task.Run(child.Stop);
        await test.Clock.WaitForTimerAsync(TimeSpan.FromMilliseconds(50), TestTimeout);
        test.Clock.Advance(TimeSpan.FromMilliseconds(50));
        await test.Clock.WaitForTimerAsync(TimeSpan.FromMilliseconds(150), TestTimeout);
        test.Clock.Advance(TimeSpan.FromMilliseconds(150));
        await end.WaitAsync(TestTimeout);
        childScope.Dispose();
        await scope.FileSession.WaitForIdleAsync().WaitAsync(TestTimeout);
        Assert.Equal(3, writes);
        Assert.Equal(TelemetryLayerCompletion.NotPublished,
            await scope.CompleteAsync(root, terminalStatus: ActivityStatusCode.Ok).WaitAsync(TestTimeout));
        Assert.True(scope.FileSession.Failed);
        Assert.Equal(0, new FileInfo(scope.FileSession.PartialPath!).Length);
        Assert.Equal(0, fileSystem.Moves);
        Assert.Empty(new TraceFileStore(test.Root).ClaimPending());
    }

    [Fact]
    public async Task UnconfirmedRollback_PreventsAnyRetry()
    {
        var writes = 0;
        var fileSystem = new TelemetryLayerTestFileSystem
        {
            Wrap = stream => new TelemetryLayerFaultStream(stream)
            {
                FailRollback = true,
                WriteAction = (inner, bytes, offset, count) =>
                {
                    Interlocked.Increment(ref writes);
                    inner.Write(bytes, offset, 7);
                    throw new IOException("Injected write failure");
                },
            },
        };
        await using var test = new TelemetryLayerTestEnvironment(clock => new TelemetryLayerScopeOptions
        {
            TimeProvider = clock, FileSystem = fileSystem, Diagnostic = _ => { },
        });
        var scope = test.Create();
        using (var activityScope = scope.Telemetry.StartIndependentRootActivity("Test.Child"))
            Assert.NotNull(activityScope?.Activity);
        Assert.Equal(TelemetryLayerCompletion.NotPublished, await scope.CompleteAsync().WaitAsync(TestTimeout));
        Assert.Equal(1, writes);
        Assert.Contains("rollback_unconfirmed", scope.Diagnostics);
        Assert.False(File.Exists(scope.FileSession.PublishedPath));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnconfirmedWriteOrHandleClose_IsNotPublished_AndNeverPublishesLater(bool blockClose)
    {
        using var io = new TelemetryLayerBarrier();
        var first = 0;
        var fileSystem = new TelemetryLayerTestFileSystem
        {
            Wrap = stream => new TelemetryLayerFaultStream(stream)
            {
                WriteAction = (inner, bytes, offset, count) =>
                {
                    if (!blockClose && Interlocked.Increment(ref first) == 1)
                        io.Hit();
                    inner.Write(bytes, offset, count);
                },
                BeforeClose = () =>
                {
                    if (blockClose && Interlocked.Increment(ref first) == 1)
                        io.Hit();
                },
            },
        };
        await using var test = new TelemetryLayerTestEnvironment(clock => new TelemetryLayerScopeOptions
        {
            TimeProvider = clock, FileSystem = fileSystem, Diagnostic = _ => { },
        });
        var scope = test.Create();
        using var childScope = scope.Telemetry.StartIndependentRootActivity("Test.Child", activate: false)!;
        var child = childScope.Activity;
        var end = Task.Run(child.Stop);
        await io.Entered.Task.WaitAsync(TestTimeout);
        var completion = scope.CompleteAsync();
        await scope.WaitForStateAsync(TelemetryLayerState.Fenced).WaitAsync(TestTimeout);
        Assert.False(completion.IsCompleted);
        Assert.Empty(new TraceFileStore(test.Root).ClaimPending());
        test.Clock.Advance(LayerTelemetryScope.FileCompletionBudget);
        Assert.Equal(TelemetryLayerCompletion.NotPublished, await completion.WaitAsync(TestTimeout));
        Assert.Equal(TelemetryLayerState.NotPublished, scope.State);
        Assert.Equal(0, fileSystem.Moves);
        io.Release();
        await end.WaitAsync(TestTimeout);
        childScope.Dispose();
        Assert.True(await scope.ProviderDisposalCompletion.WaitAsync(TestTimeout));
        Assert.Equal(0, fileSystem.Moves);
        Assert.False(File.Exists(scope.FileSession.PublishedPath));
        Assert.True(File.Exists(scope.FileSession.PartialPath));
    }

    [Fact]
    public async Task AcceptedWriteBeforeIo_IsCounted_AndCannotCreateAFileAfterTimeout()
    {
        using var accepted = new TelemetryLayerBarrier();
        var fileSystem = new TelemetryLayerTestFileSystem();
        await using var test = new TelemetryLayerTestEnvironment(clock => new TelemetryLayerScopeOptions
        {
            TimeProvider = clock, FileSystem = fileSystem, Diagnostic = _ => { },
            Checkpoint = point =>
            {
                if (point == TelemetryLayerCheckpoint.WriteAccepted)
                    accepted.Hit();
            },
        });
        var scope = test.Create();
        using var childScope = scope.Telemetry.StartIndependentRootActivity("Test.Child", activate: false)!;
        var child = childScope.Activity;
        var end = Task.Run(child.Stop);
        await accepted.Entered.Task.WaitAsync(TestTimeout);
        var completion = scope.CompleteAsync();
        await scope.WaitForStateAsync(TelemetryLayerState.Fenced).WaitAsync(TestTimeout);
        test.Clock.Advance(LayerTelemetryScope.FileCompletionBudget);
        Assert.Equal(TelemetryLayerCompletion.NotPublished, await completion.WaitAsync(TestTimeout));
        accepted.Release();
        await end.WaitAsync(TestTimeout);
        childScope.Dispose();
        Assert.True(await scope.ProviderDisposalCompletion.WaitAsync(TestTimeout));
        Assert.Equal(0, fileSystem.Directories);
        Assert.Equal(0, fileSystem.Opens);
        Assert.False(Directory.Exists(test.Root));
    }

    [Fact]
    public async Task FinalRootWrite_UsesTheFileBudgetRatherThanAnotherProducerWait()
    {
        using var io = new TelemetryLayerBarrier();
        var fileSystem = new TelemetryLayerTestFileSystem
        {
            Wrap = stream => new TelemetryLayerFaultStream(stream)
            {
                WriteAction = (inner, bytes, offset, count) =>
                {
                    io.Hit();
                    inner.Write(bytes, offset, count);
                },
            },
        };
        await using var test = new TelemetryLayerTestEnvironment(clock => new TelemetryLayerScopeOptions
        {
            TimeProvider = clock, FileSystem = fileSystem, Diagnostic = _ => { },
        });
        var scope = test.Create();
        using var rootScope = scope.Telemetry.StartIndependentRootActivity("Test.Root", activate: false);
        var root = rootScope?.Activity;
        var completion = scope.CompleteAsync(root, terminalStatus: ActivityStatusCode.Error);
        await io.Entered.Task.WaitAsync(TestTimeout);
        try
        {
            Assert.Equal(TelemetryLayerState.Fenced, scope.State);
            test.Clock.Advance(LayerTelemetryScope.FileCompletionBudget);
            Assert.Equal(TelemetryLayerCompletion.NotPublished, await completion.WaitAsync(TestTimeout));
            Assert.Equal(0, fileSystem.Moves);
        }
        finally
        {
            io.Release();
        }
        Assert.True(await scope.ProviderDisposalCompletion.WaitAsync(TestTimeout));
        Assert.False(File.Exists(scope.FileSession.PublishedPath));
    }

    [Fact]
    public async Task UnconfirmedCapture_DoesNotCreateAFileAfterTheFileBudget()
    {
        using var capture = new TelemetryLayerBarrier();
        var fileSystem = new TelemetryLayerTestFileSystem();
        await using var test = new TelemetryLayerTestEnvironment(clock => new TelemetryLayerScopeOptions
        {
            TimeProvider = clock, FileSystem = fileSystem, Diagnostic = _ => { },
            Checkpoint = point =>
            {
                if (point == TelemetryLayerCheckpoint.CaptureReserved)
                    capture.Hit();
            },
        });
        var scope = test.Create();
        using var childScope = scope.Telemetry.StartIndependentRootActivity("Test.Child", activate: false)!;
        var child = childScope.Activity;
        var end = Task.Run(child.Stop);
        await capture.Entered.Task.WaitAsync(TestTimeout);
        var completion = scope.CompleteAsync();
        test.Clock.Advance(LayerTelemetryScope.FileCompletionBudget);
        await scope.WaitForStateAsync(TelemetryLayerState.Fenced).WaitAsync(TestTimeout);
        Assert.Equal(TelemetryLayerCompletion.NotPublished, await completion.WaitAsync(TestTimeout));
        capture.Release();
        await end.WaitAsync(TestTimeout);
        childScope.Dispose();
        Assert.True(await scope.ProviderDisposalCompletion.WaitAsync(TestTimeout));
        Assert.Equal(0, fileSystem.Directories);
        Assert.Equal(0, fileSystem.Opens);
        Assert.False(Directory.Exists(test.Root));
    }

    [Fact]
    public async Task Rename_RetriesAtMostThreeTimes_WithinTheOriginalFileBudget()
    {
        var attempts = 0;
        var fileSystem = new TelemetryLayerTestFileSystem
        {
            BeforeMove = () =>
            {
                if (Interlocked.Increment(ref attempts) < 3)
                    throw new IOException("Injected rename failure");
            },
        };
        await using var test = new TelemetryLayerTestEnvironment(clock => new TelemetryLayerScopeOptions
        {
            TimeProvider = clock, FileSystem = fileSystem, Diagnostic = _ => { },
        });
        var scope = test.Create();
        using (scope.Telemetry.StartIndependentRootActivity("Test.Child")) { }
        var completion = scope.CompleteAsync();
        await test.Clock.WaitForTimerAsync(TimeSpan.FromMilliseconds(50), TestTimeout);
        test.Clock.Advance(TimeSpan.FromMilliseconds(50));
        await test.Clock.WaitForTimerAsync(TimeSpan.FromMilliseconds(150), TestTimeout);
        test.Clock.Advance(TimeSpan.FromMilliseconds(150));
        Assert.Equal(TelemetryLayerCompletion.Published, await completion.WaitAsync(TestTimeout));
        Assert.Equal(3, attempts);
        Assert.Single(test.Read(scope));
    }

    [Fact]
    public async Task TimedOutPreRenameWork_CannotPublishAfterCompleteReturns()
    {
        using var rename = new TelemetryLayerBarrier();
        var fileSystem = new TelemetryLayerTestFileSystem();
        await using var test = new TelemetryLayerTestEnvironment(clock => new TelemetryLayerScopeOptions
        {
            TimeProvider = clock, FileSystem = fileSystem, Diagnostic = _ => { },
            Checkpoint = point =>
            {
                if (point == TelemetryLayerCheckpoint.BeforeRename)
                    rename.Hit();
            },
        });
        var scope = test.Create();
        using (scope.Telemetry.StartIndependentRootActivity("Test.Child")) { }
        var completion = scope.CompleteAsync();
        await rename.Entered.Task.WaitAsync(TestTimeout);
        Assert.Equal(TelemetryLayerState.Sealed, scope.State);
        test.Clock.Advance(LayerTelemetryScope.FileCompletionBudget);
        Assert.Equal(TelemetryLayerCompletion.NotPublished, await completion.WaitAsync(TestTimeout));
        rename.Release();
        Assert.NotNull(scope.FileSession.PublicationCompletion);
        Assert.False(await scope.FileSession.PublicationCompletion.WaitAsync(TestTimeout));
        Assert.Equal(0, fileSystem.Moves);
        Assert.True(File.Exists(scope.FileSession.PartialPath));
        Assert.False(File.Exists(scope.FileSession.PublishedPath));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdmittedMove_ReportsUnconfirmedUntilItsActualResultIsKnown(bool failMove)
    {
        using var move = new TelemetryLayerBarrier();
        var fileSystem = new TelemetryLayerTestFileSystem
        {
            BeforeMove = () =>
            {
                move.Hit();
                if (failMove)
                    throw new IOException("Injected admitted rename failure");
            },
        };
        await using var test = new TelemetryLayerTestEnvironment(clock => new TelemetryLayerScopeOptions
        {
            TimeProvider = clock, FileSystem = fileSystem, Diagnostic = _ => { },
        });
        var scope = test.Create();
        using (scope.Telemetry.StartIndependentRootActivity("Test.Child")) { }
        var completion = scope.CompleteAsync();
        await move.Entered.Task.WaitAsync(TestTimeout);
        try
        {
            test.Clock.Advance(LayerTelemetryScope.FileCompletionBudget);
            Assert.Equal(TelemetryLayerCompletion.PublicationUnconfirmed, await completion.WaitAsync(TestTimeout));
            Assert.Equal(TelemetryLayerState.PublicationUnconfirmed, scope.State);
            Assert.Contains("publication_unconfirmed", scope.Diagnostics);
            Assert.False(scope.FileSession.Failed);
            Assert.False(scope.FileSession.Published);
            Assert.True(File.Exists(scope.FileSession.PartialPath));
            Assert.False(File.Exists(scope.FileSession.PublishedPath));
            Assert.Equal(1, fileSystem.Moves);
        }
        finally
        {
            move.Release();
            if (scope.FileSession.PublicationCompletion is { } publication)
                await publication.WaitAsync(TestTimeout);
        }

        var expectedState = failMove ? TelemetryLayerState.NotPublished : TelemetryLayerState.Published;
        await scope.WaitForStateAsync(expectedState).WaitAsync(TestTimeout);
        Assert.Equal(expectedState, scope.State);
        Assert.NotNull(scope.FileSession.PublicationCompletion);
        Assert.Equal(!failMove, await scope.FileSession.PublicationCompletion.WaitAsync(TestTimeout));
        Assert.Equal(!failMove, scope.FileSession.Published);
        Assert.Equal(failMove, scope.FileSession.Failed);
        Assert.Equal(failMove, File.Exists(scope.FileSession.PartialPath));
        Assert.Equal(!failMove, File.Exists(scope.FileSession.PublishedPath));
        Assert.Equal(1, fileSystem.Moves);
        Assert.Equal(TelemetryLayerCompletion.PublicationUnconfirmed, await scope.CompleteAsync());
    }

    [Fact]
    public async Task CompletedMoveFailureAtDeadline_IsNotAnUnconfirmedCommit()
    {
        var fileSystem = new TelemetryLayerTestFileSystem
        {
            BeforeMove = () => throw new IOException("Injected completed rename failure"),
        };
        await using var test = new TelemetryLayerTestEnvironment(clock => new TelemetryLayerScopeOptions
        {
            TimeProvider = clock, FileSystem = fileSystem, Diagnostic = _ => { },
        });
        var scope = test.Create();
        using (scope.Telemetry.StartIndependentRootActivity("Test.Child")) { }
        var completion = scope.CompleteAsync();
        await test.Clock.WaitForTimerAsync(TimeSpan.FromMilliseconds(50), TestTimeout);
        Assert.Equal(1, fileSystem.Moves);
        test.Clock.Advance(LayerTelemetryScope.FileCompletionBudget);
        Assert.Equal(TelemetryLayerCompletion.NotPublished, await completion.WaitAsync(TestTimeout));
        Assert.NotNull(scope.FileSession.PublicationCompletion);
        Assert.False(await scope.FileSession.PublicationCompletion.WaitAsync(TestTimeout));
        Assert.Equal(TelemetryLayerState.NotPublished, scope.State);
        Assert.DoesNotContain("publication_unconfirmed", scope.Diagnostics);
        Assert.False(scope.FileSession.Published);
        Assert.True(scope.FileSession.Failed);
        Assert.True(File.Exists(scope.FileSession.PartialPath));
        Assert.False(File.Exists(scope.FileSession.PublishedPath));
        Assert.Equal(1, fileSystem.Moves);
    }

    [Fact]
    public async Task FailureBeforeCommitAdmission_IsNotPublished()
    {
        var fileSystem = new TelemetryLayerTestFileSystem();
        await using var test = new TelemetryLayerTestEnvironment(clock => new TelemetryLayerScopeOptions
        {
            TimeProvider = clock, FileSystem = fileSystem, Diagnostic = _ => { },
            Checkpoint = point =>
            {
                if (point == TelemetryLayerCheckpoint.BeforeRename)
                    throw new IOException("Injected pre-admission failure");
            },
        });
        var scope = test.Create();
        using (scope.Telemetry.StartIndependentRootActivity("Test.Child")) { }
        Assert.Equal(TelemetryLayerCompletion.NotPublished, await scope.CompleteAsync().WaitAsync(TestTimeout));
        Assert.DoesNotContain("publication_unconfirmed", scope.Diagnostics);
        Assert.Equal(0, fileSystem.Moves);
        Assert.False(scope.FileSession.Published);
        Assert.True(scope.FileSession.Failed);
        Assert.True(File.Exists(scope.FileSession.PartialPath));
        Assert.False(File.Exists(scope.FileSession.PublishedPath));
    }

    [Fact]
    public async Task SeparateGuids_OnlyPublishTheirOwnFile()
    {
        await using var test = new TelemetryLayerTestEnvironment();
        var other = new TelemetryLayer("OtherLayer" + Guid.NewGuid().ToString("N"), "testlayer", "other-v1",
            ["Test.Root", "Test.Child"]);
        var first = test.Create();
        var second = test.Create(factory: test.NewFactory(other), layer: other.ServiceName);
        using (first.Telemetry.StartIndependentRootActivity("Test.Child")) { }
        using (second.Telemetry.StartIndependentRootActivity("Test.Child")) { }
        await Task.WhenAll(first.FileSession.WaitForIdleAsync(), second.FileSession.WaitForIdleAsync())
            .WaitAsync(TestTimeout);
        Assert.NotEqual(first.FileSession.FileId, second.FileSession.FileId);
        Assert.NotEqual(first.Context.InvocationId, first.FileSession.FileId);
        Assert.True(File.Exists(first.FileSession.PartialPath));
        Assert.True(File.Exists(second.FileSession.PartialPath));
        Assert.Equal(TelemetryLayerCompletion.Published, await first.CompleteAsync().WaitAsync(TestTimeout));
        Assert.True(File.Exists(second.FileSession.PartialPath));
        Assert.False(File.Exists(second.FileSession.PublishedPath));
        Assert.Single(new TraceFileStore(test.Root).ClaimPending());
        Assert.Equal(TelemetryLayerCompletion.Published, await second.CompleteAsync().WaitAsync(TestTimeout));
        Assert.Single(new TraceFileStore(test.Root).ClaimPending());
    }
}
