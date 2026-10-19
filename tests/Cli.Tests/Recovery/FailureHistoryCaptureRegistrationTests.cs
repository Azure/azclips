// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Recovery;
using Core.Runtime.Recovery;
using Moq;
using static Cli.Tests.Recovery.FailureHistoryTestScope;

namespace Cli.Tests.Recovery;

[Collection("Azclips Service Provider Tests")]
public sealed class FailureHistoryCaptureRegistrationTests
{
    [Fact]
    public async Task OffWithoutHistory_TouchesNoFiles()
    {
        using var scope = new FailureHistoryTestScope(trigger: "off");
        var store = scope.CreateStore(NoKeyAccess());

        await store.ApplyPolicyAsync();
        Assert.Null(await store.BeginCaptureAsync());

        Assert.False(Directory.Exists(scope.DirectoryPath));
    }

    [Fact]
    public async Task ReadonlyGenerationZero_StillNeedsTheFirstOffFenceOnceHistoryStateExists()
    {
        using var scope = new FailureHistoryTestScope();
        var store = scope.CreateStore();
        var oldGeneration = await store.ReadGenerationAsync();
        Assert.Equal(0UL, oldGeneration);
        Directory.CreateDirectory(scope.DirectoryPath);

        await SetFileTriggerAsync(scope, "off");
        await store.ApplyPolicyAsync();
        var fenced = (await scope.State.ReadAsync(default))!;
        Assert.Equal(1UL, fenced.Generation);
        Assert.True(fenced.OffFenced);
        Assert.Equal(0UL, fenced.NextSequence);
        Assert.Null(await store.BeginCaptureAsync());
        Assert.Equal(fenced, await scope.State.ReadAsync(default));

        await SetFileTriggerAsync(scope, "manual");
        Assert.Equal("failure_history_stale_generation",
            (await Assert.ThrowsAsync<FailureHistoryException>(() => store.SaveAsync(CreateRecord(oldGeneration), 10))).Code);
        var generation = await store.BeginCaptureAsync();
        Assert.Equal(fenced.Generation + 1, generation);
        await store.SaveAsync(CreateRecord(generation!.Value), 10);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmptyRegisteredCapture_InvalidatesCompletedOffForFileAndEnvironmentPolicies(bool useEnvironment)
    {
        const string variable = "AZCLIPS_AGENT_RECOVERY";
        var previous = Environment.GetEnvironmentVariable(variable);
        Environment.SetEnvironmentVariable(variable, null);
        try
        {
            using var scope = new FailureHistoryTestScope();
            var keys = NoKeyAccess();
            var store = scope.CreateStore(keys);
            await scope.Configuration.SetConfigurationAsync("agent.recovery", "off");
            var fenced = (await scope.State.ReadAsync(default))!;
            if (useEnvironment)
                Environment.SetEnvironmentVariable(variable, "manual");
            else
                await SetFileTriggerAsync(scope, "manual");

            var generation = (await store.BeginCaptureAsync())!.Value;
            Assert.Equal(fenced.Generation + 1, generation);
            Assert.False((await scope.State.ReadAsync(default))!.OffFenced);
            Assert.False(File.Exists(scope.Files.HistoryPath));
            if (useEnvironment)
                Environment.SetEnvironmentVariable(variable, null);
            else
                await SetFileTriggerAsync(scope, "off");

            await store.ApplyPolicyAsync();
            var reset = (await scope.State.ReadAsync(default))!;
            Assert.Equal(generation + 1, reset.Generation);
            Assert.Equal(fenced.NextSequence, reset.NextSequence);
            Assert.True(reset.OffFenced);
            await store.ApplyPolicyAsync();
            Assert.Equal(reset, await scope.State.ReadAsync(default));
            await SetFileTriggerAsync(scope, "manual");
            Assert.Equal("failure_history_stale_generation",
                (await Assert.ThrowsAsync<FailureHistoryException>(() => store.SaveAsync(CreateRecord(generation), 10))).Code);
            Assert.Empty(keys.CreateRequests);
        }
        finally { Environment.SetEnvironmentVariable(variable, previous); }
    }

    [Fact]
    public async Task ConcurrentFirstOffObservers_CommitOnlyOneFence()
    {
        var writes = 0;
        using var scope = new FailureHistoryTestScope(replace: (source, target) =>
        {
            if (Path.GetFileName(target) == EncryptedFailureHistoryStore.ControlFileName)
                Interlocked.Increment(ref writes);
            File.Move(source, target, overwrite: true);
        });
        Directory.CreateDirectory(scope.DirectoryPath);
        await SetFileTriggerAsync(scope, "off");
        var keys = NoKeyAccess();
        await Task.WhenAll(Enumerable.Range(0, 12).Select(_ =>
            Task.Run(() => scope.CreateStore(keys).ApplyPolicyAsync())));
        var state = (await scope.State.ReadAsync(default))!;
        Assert.Equal(1UL, state.Generation);
        Assert.True(state.OffFenced);
        Assert.Equal(2, writes);
        Assert.Empty(keys.CreateRequests);
    }

    [Fact]
    public async Task BeginCapture_PolicyProbeCannotAdoptANewerGeneration()
    {
        using var scope = new FailureHistoryTestScope();
        var probed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reads = 0;
        var store = scope.CreateStore(NoKeyAccess(), Policy(() =>
        {
            if (Interlocked.Increment(ref reads) == 1)
            {
                probed.SetResult();
                resume.Task.GetAwaiter().GetResult();
            }
            return "manual";
        }));
        var begin = Task.Run(() => store.BeginCaptureAsync());
        Exception? failure;
        try
        {
            await probed.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await scope.Configuration.SetConfigurationAsync("agent.recovery", "off");
            await scope.Configuration.SetConfigurationAsync("agent.recovery", "manual");
        }
        finally
        {
            resume.TrySetResult();
            failure = await Record.ExceptionAsync(() => begin);
        }
        Assert.Equal("failure_history_stale_generation", Assert.IsType<FailureHistoryException>(failure).Code);
        var fresh = scope.CreateStore();
        var generation = (await fresh.BeginCaptureAsync())!.Value;
        await fresh.SaveAsync(CreateRecord(generation), 10);
    }

    [Fact]
    public async Task ObservedOff_FencesItsGenerationEvenIfPolicyChangesBeforeTheLockedRead()
    {
        using var scope = new FailureHistoryTestScope();
        var store = scope.CreateStore(NoKeyAccess());
        var generation = (await store.BeginCaptureAsync())!.Value;
        var reads = 0;
        var observer = scope.CreateStore(NoKeyAccess(),
            Policy(() => Interlocked.Increment(ref reads) == 1 ? "off" : "manual"));
        await observer.ApplyPolicyAsync();
        Assert.True(await store.ReadGenerationAsync() > generation);
        Assert.Equal("failure_history_stale_generation",
            (await Assert.ThrowsAsync<FailureHistoryException>(() => store.SaveAsync(CreateRecord(generation), 10))).Code);
    }

    [Fact]
    public async Task DelayedOffObservation_DoesNotClearAnAlreadyFencedNewGeneration()
    {
        using var scope = new FailureHistoryTestScope();
        var store = scope.CreateStore();
        await store.BeginCaptureAsync();
        var observed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reads = 0;
        var observer = scope.CreateStore(NoKeyAccess(), Policy(() =>
        {
            if (Interlocked.Increment(ref reads) != 1)
                return "manual";
            observed.SetResult();
            resume.Task.GetAwaiter().GetResult();
            return "off";
        }));
        var observation = Task.Run(() => observer.ApplyPolicyAsync());
        FailureHistoryEntry entry;
        try
        {
            await observed.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await scope.Configuration.SetConfigurationAsync("agent.recovery", "off");
            await scope.Configuration.SetConfigurationAsync("agent.recovery", "manual");
            var generation = (await store.BeginCaptureAsync())!.Value;
            entry = await store.SaveAsync(CreateRecord(generation), 10);
        }
        finally
        {
            resume.TrySetResult();
            await observation;
        }
        Assert.Equal(entry, Assert.Single(await store.ReadIndexAsync()));
    }

    [Fact]
    public async Task RepeatedExplicitOff_IsIdempotentButReenableStillFences()
    {
        using var scope = new FailureHistoryTestScope();
        var store = scope.CreateStore(NoKeyAccess());
        await scope.Configuration.SetConfigurationAsync("agent.recovery", "off");
        var first = (await scope.State.ReadAsync(default))!;
        await scope.Configuration.SetConfigurationAsync("agent.recovery", "off");
        Assert.Equal(first, await scope.State.ReadAsync(default));
        await scope.Configuration.SetConfigurationAsync("agent.recovery", "manual");
        Assert.Equal(first.Generation + 1, await store.ReadGenerationAsync());
        Assert.False((await scope.State.ReadAsync(default))!.OffFenced);
        var generation = (await store.BeginCaptureAsync())!.Value;
        await scope.Configuration.SetConfigurationAsync("agent.recovery", "off");
        await scope.Configuration.SetConfigurationAsync("agent.recovery", "manual");
        Assert.Equal("failure_history_stale_generation",
            (await Assert.ThrowsAsync<FailureHistoryException>(() => store.SaveAsync(CreateRecord(generation), 10))).Code);
    }

    [Fact]
    public async Task LegacyControlWithoutFenceProof_IsNotMistakenForCompletedCleanup()
    {
        using var scope = new FailureHistoryTestScope();
        Directory.CreateDirectory(scope.DirectoryPath);
        await File.WriteAllTextAsync(scope.Files.ControlPath,
            """{"version":1,"nextSequence":73,"generation":4,"clearPending":false}""");
        await SetFileTriggerAsync(scope, "off");
        await scope.CreateStore(NoKeyAccess()).ApplyPolicyAsync();
        var state = (await scope.State.ReadAsync(default))!;
        Assert.Equal(5UL, state.Generation);
        Assert.Equal(73UL, state.NextSequence);
        Assert.True(state.OffFenced);
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(scope.Files.ControlPath));
        Assert.Equal(new[] { "clearPending", "generation", "nextSequence", "offFenced", "version" },
            document.RootElement.EnumerateObject().Select(property => property.Name).Order());
    }

    [Fact]
    public async Task ControlledFilesOverrideACompletedFenceProof()
    {
        using var scope = new FailureHistoryTestScope();
        var store = scope.CreateStore(NoKeyAccess());
        await scope.Configuration.SetConfigurationAsync("agent.recovery", "off");
        var generation = await store.ReadGenerationAsync();
        var leftover = Path.Combine(scope.DirectoryPath, EncryptedFailureHistoryStore.LegacyHistoryFileName);
        await File.WriteAllTextAsync(leftover, "old draft bytes");
        await store.ApplyPolicyAsync();
        Assert.False(File.Exists(leftover));
        Assert.Equal(generation + 1, await store.ReadGenerationAsync());
    }

    [Fact]
    public async Task ClosedGeneration_CannotBeWrittenOrReusedWhenEnabled()
    {
        using var scope = new FailureHistoryTestScope();
        var keys = new TestFailureHistoryKeys();
        var store = scope.CreateStore(keys);
        await scope.Configuration.SetConfigurationAsync("agent.recovery", "off");
        var closed = (await scope.State.ReadAsync(default))!;
        var unregistered = CreateRecord(await store.ReadGenerationAsync());
        await SetFileTriggerAsync(scope, "manual");
        Assert.Equal("failure_history_stale_generation",
            (await Assert.ThrowsAsync<FailureHistoryException>(() => store.SaveAsync(unregistered, 10))).Code);
        Assert.Equal(closed, await scope.State.ReadAsync(default));
        Assert.Empty(keys.CreateRequests);

        var generation = (await store.BeginCaptureAsync())!.Value;
        Assert.Equal(closed.Generation + 1, generation);
        Assert.False((await scope.State.ReadAsync(default))!.OffFenced);
        Assert.Equal("failure_history_stale_generation",
            (await Assert.ThrowsAsync<FailureHistoryException>(() => store.SaveAsync(unregistered, 10))).Code);
        await store.SaveAsync(CreateRecord(generation), 10);
    }

    [Fact]
    public async Task ExhaustedClosedGeneration_CannotClearTheFenceOrWrap()
    {
        using var scope = new FailureHistoryTestScope();
        await scope.Configuration.SetConfigurationAsync("agent.recovery", "off");
        var state = (await scope.State.ReadAsync(default))! with { Generation = ulong.MaxValue };
        await scope.State.WriteAsync(state, default);
        var store = scope.CreateStore(NoKeyAccess());
        await store.ApplyPolicyAsync();
        await Assert.ThrowsAsync<FailureHistoryException>(() => scope.Configuration.SetConfigurationAsync("agent.recovery", "manual"));
        Assert.Equal("off", scope.Configuration.GetConfiguration<AgentOptions>().Recovery.Trigger);
        await SetFileTriggerAsync(scope, "manual");
        Assert.Equal("failure_history_generation_exhausted",
            (await Assert.ThrowsAsync<FailureHistoryException>(() => store.BeginCaptureAsync())).Code);
        Assert.Equal(state, await scope.State.ReadAsync(default));
    }

    [Theory]
    [InlineData(0UL, false)]
    [InlineData(1UL, true)]
    public async Task ContradictoryFenceMetadata_IsCorruptRatherThanClean(ulong generation, bool pending)
    {
        using var scope = new FailureHistoryTestScope();
        Directory.CreateDirectory(scope.DirectoryPath);
        await scope.State.WriteAsync(new FailureHistoryControl
        {
            Generation = generation, ClearPending = pending, OffFenced = true,
        }, default);
        var store = scope.CreateStore(NoKeyAccess());
        Assert.Equal("failure_history_control_corrupt",
            (await Assert.ThrowsAsync<FailureHistoryException>(() => store.ReadGenerationAsync())).Code);
        await Assert.ThrowsAsync<FailureHistoryException>(() => store.ApplyPolicyAsync());
        await Assert.ThrowsAsync<FailureHistoryException>(() => store.BeginCaptureAsync());
    }

    private static Task SetFileTriggerAsync(FailureHistoryTestScope scope, string trigger) =>
        scope.Provider.WriteAsync(JsonSerializer.SerializeToNode(new { agent = new { recovery = new { trigger } } })!.AsObject());

    private static TestFailureHistoryKeys NoKeyAccess() =>
        new() { Read = (_, _, _) => throw new InvalidOperationException("Capture registration and off cleanup must be keyless.") };

    private static IConfigurationService Policy(Func<string> trigger)
    {
        var configuration = new Mock<IConfigurationService>(MockBehavior.Strict);
        configuration.Setup(service => service.GetConfiguration<AgentOptions>(null))
            .Returns(() => new AgentOptions { Recovery = new RecoveryOptions { Trigger = trigger() } });
        return configuration.Object;
    }
}
