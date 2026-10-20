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
        await Assert.ThrowsAsync<FailureHistoryException>(() => scope.Configuration.SetConfigurationAsync("agent.recovery", "manual"));
        Assert.Equal("off", scope.Configuration.GetConfiguration<AgentOptions>().Recovery);
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
        await Assert.ThrowsAsync<FailureHistoryException>(() => store.BeginCaptureAsync());
    }

    private static Task SetFileTriggerAsync(FailureHistoryTestScope scope, string trigger) =>
        scope.Provider.WriteAsync(JsonSerializer.SerializeToNode(new { agent = new { recovery = trigger } })!.AsObject());

    private static TestFailureHistoryKeys NoKeyAccess() =>
        new() { Read = (_, _, _) => throw new InvalidOperationException("Capture registration and off cleanup must be keyless.") };

    private static IConfigurationService Policy(Func<string> trigger)
    {
        var configuration = new Mock<IConfigurationService>(MockBehavior.Strict);
        configuration.Setup(service => service.GetConfiguration<AgentOptions>(null))
            .Returns(() => new AgentOptions { Recovery = trigger() });
        return configuration.Object;
    }
}
