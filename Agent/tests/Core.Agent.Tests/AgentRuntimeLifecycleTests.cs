// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
using Core.Agent.AgentMode;
using Core.Agent.Hosting;
using Core.Agent.Providers;
using Core.Agent.Telemetry;
using Core.Agent.Tools.Help;
using Core.Agent.Tools.Resource.Cache;
using Core.Telemetry;
using GitHub.Copilot;
using GitHub.Copilot.Rpc;
using Microsoft.Extensions.AI;

namespace Core.Agent.Tests;

[Collection("AgentTelemetry")]
public sealed class AgentRuntimeLifecycleTests
{
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    [Theory]
    [InlineData("tool")]
    [InlineData("hook")]
    [InlineData("permission")]
    [InlineData("event")]
    public async Task CallbacksBeforeAnyActivity_HonorHostStop_AndOwnTheirActualFinally(string kind)
    {
        await using var harness = new AgentLayerTestHarness();
        var scope = harness.CreateScope();
        using var context = new AgentInvocationContext(null, scope.Telemetry, scope);
        var entered = Signal();
        var cancelled = Signal();
        var releaseFinally = Signal();
        var exited = false;
        var enteredWithoutActivity = false;

        async Task<string> Work(CancellationToken token)
        {
            enteredWithoutActivity = Activity.Current is null;
            entered.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return "done";
            }
            catch (OperationCanceledException)
            {
                cancelled.TrySetResult();
                throw;
            }
            finally
            {
                await releaseFinally.Task;
                exited = true;
            }
        }

        var config = new SessionConfig
        {
            Tools = [AIFunctionFactory.Create(Work)],
            Hooks = new SessionHooks
            {
                OnSessionStart = async (_, _) =>
                {
                    await Work(context.CallbackCancellationToken);
                    return new SessionStartHookOutput();
                },
            },
            OnPermissionRequest = async (_, _) =>
            {
                await Work(context.CallbackCancellationToken);
#pragma warning disable GHCP001 // Exercise the SDK permission callback with an offline response.
                return PermissionDecision.ApproveOnce();
#pragma warning restore GHCP001
            },
            OnEvent = _ => Work(context.CallbackCancellationToken).GetAwaiter().GetResult(),
        };
        context.Bind(config);
        Task callback = kind switch
        {
            "tool" => config.Tools!.Single().GetService<AIFunction>()!.InvokeAsync(new AIFunctionArguments()).AsTask(),
            "hook" => config.Hooks!.OnSessionStart!(new SessionStartHookInput(), new HookInvocation { SessionId = "offline" }),
            "permission" => config.OnPermissionRequest!(null!, null!),
            _ => Task.Run(() => config.OnEvent!(new SessionIdleEvent { Data = new SessionIdleData() })),
        };
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(enteredWithoutActivity);

        using var rootScope = scope.Telemetry.StartIndependentRootActivity("Agent.Session");
        var root = rootScope?.Activity;
        var completion = scope.CompleteAsync(root, terminalStatus: ActivityStatusCode.Ok);
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(completion.IsCompleted);
        Assert.False(exited);
        releaseFinally.SetResult();

        if (kind == "event")
            await callback;
        else
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => callback);
        Assert.Equal(TelemetryLayerCompletion.Published, await completion);
        Assert.True(exited);
        Assert.Single(harness.ReadEntries());
    }

    [Fact]
    public async Task SdkTeardown_AllowsItsTrackedHooksAndNestedTools()
    {
        await using var harness = new AgentLayerTestHarness();
        var scope = harness.CreateScope();
        using var rootScope = scope.Telemetry.StartIndependentRootActivity("Agent.Session");
        var root = rootScope?.Activity;
        var context = new AgentInvocationContext(root, scope.Telemetry, scope);
        var lifetime = new AgentRuntimeLifetime(scope);
        var resource = new AgentSdkLifetime(context);
        var nestedResult = "";
        var sawHostStop = false;
        var config = new SessionConfig
        {
            Tools = [new HelpTools("must-not-run", context.StartToolActivity).GetCommand],
        };
        config.Hooks = new SessionHooks
        {
            OnSessionStart = async (_, _) =>
            {
                using var tool = ToolInvocationLog.Start("teardown", activityCreator: context.StartToolActivity);
                try { await Task.Delay(Timeout.InfiniteTimeSpan, context.CallbackCancellationToken); }
                catch (OperationCanceledException) { sawHostStop = true; }
                nestedResult = (await config.Tools.Single().GetService<AIFunction>()!
                    .InvokeAsync(new AIFunctionArguments { ["name"] = "" }))!.ToString()!;
                return new SessionStartHookOutput();
            },
        };
        context.Bind(config);
        resource.SetClientCleanup(async () =>
            await config.Hooks.OnSessionStart!(new SessionStartHookInput(), new HookInvocation { SessionId = "offline" }));
        lifetime.RegisterCleanup(resource.DisposeAsync);

        Assert.Equal(TelemetryLayerCompletion.Published, await lifetime.CompleteAsync(root,
            new Dictionary<string, object?> { [TelemetryTags.RecoveryOutcome] = "ok" }, ActivityStatusCode.Ok));

        Assert.True(sawHostStop);
        Assert.Contains("missing_name", nestedResult);
        Assert.Throws<ObjectDisposedException>(() => context.StartOperation("Agent.Send"));
        var entries = harness.ReadEntries();
        var outer = Assert.Single(entries, entry => entry.GetProperty("OperationName").GetString() == "tool.teardown");
        var inner = Assert.Single(entries, entry => entry.GetProperty("OperationName").GetString() == "tool.get_command");
        Assert.Equal(outer.GetProperty("SpanId").GetString(), inner.GetProperty("ParentSpanId").GetString());
    }

    [Fact]
    public async Task CreateFailure_WithUncooperativeClientCleanup_UsesTheSharedDeadline()
    {
        var clock = new AgentTestTimeProvider();
        await using var harness = new AgentLayerTestHarness(timeProvider: clock);
        var scope = harness.CreateScope();
        var cleanupEntered = Signal();
        var releaseClient = Signal();
        AgentSdkLifetime? resource = null;
        AgentInvocationContext? context = null;
        var actualCleanupFinished = false;
        var running = AgentModeEntry.RunSessionAsync(() => scope, (session, lifetime) =>
        {
            context = new AgentInvocationContext(session, scope.Telemetry, scope);
            resource = new AgentSdkLifetime(context);
            resource.SetClientCleanup(async () =>
            {
                cleanupEntered.SetResult();
                await releaseClient.Task;
                actualCleanupFinished = true;
            });
            lifetime.RegisterCleanup(resource.DisposeAsync);
            throw new InvalidOperationException("original offline create failure");
        }, () => { });
        await cleanupEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(running.IsCompleted);

        clock.Advance(TimeSpan.FromSeconds(10));
        Assert.Equal(1, await running.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.False(actualCleanupFinished);
        var root = Assert.Single(harness.ReadEntries());
        var tags = AgentLayerTestHarness.Tags(root);
        Assert.Equal("error", tags[TelemetryTags.RecoveryOutcome].GetString());
        Assert.Equal("host_fault", tags[TelemetryTags.RecoveryFailureReason].GetString());
        Assert.Equal("deadline", tags[TelemetryLayerTags.Completion].GetString());
        Assert.True(tags[TelemetryLayerTags.PendingProducers].GetInt32() > 0);
        var bytes = File.ReadAllBytes(scope.FileSession.PublishedPath!);

        Assert.Null(scope.Telemetry.StartActivity("Agent.Send"));
        releaseClient.SetResult();
        await resource!.DisposeAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(actualCleanupFinished);
        Assert.Throws<ObjectDisposedException>(() => context!.StartOperation("Agent.Send"));
        Assert.Equal(bytes, File.ReadAllBytes(scope.FileSession.PublishedPath!));
    }

    [Fact]
    public async Task ActualCreateFailure_PreservesTheSdkError_AndPublishesItsSession()
    {
        await using var harness = new AgentLayerTestHarness();
        var scope = harness.CreateScope();
        await using var server = new OfflineCopilotServer { RejectCreate = true };
        var auth = new AgentOfflineAuth();
        var renderer = new SilentRenderer();
        Exception? original = null;
        var code = await AgentModeEntry.RunSessionAsync(() => scope, async (_, lifetime) =>
        {
            try
            {
                await ChatAgent.CreateAsync("offline", auth, new AgentOfflineProfiles(),
                    "must-not-run", new ConsoleApprovalPrompt(renderer: renderer), renderer,
                    new AgentOptions(), "oneshot", lifetime,
                    preAcquiredAuth: AgentOfflineAuth.Token(),
                    clientFactory: () => new CopilotClient(new CopilotClientOptions
                    {
                        Connection = RuntimeConnection.ForUri(server.Url),
                    }));
            }
            catch (Exception ex)
            {
                original = ex;
                throw;
            }
        }, () => { });

        Assert.Equal(1, code);
        Assert.Equal(1, server.CreateCount);
        Assert.NotNull(original);
        Assert.Contains("offline create failure", original.Message);
        Assert.Contains(nameof(ChatAgent.CreateAsync), original.StackTrace);
        Assert.Equal(0, auth.AcquireCount);
        var session = Assert.Single(harness.ReadEntries(), entry => entry.GetProperty("OperationName").GetString() == "Agent.Session");
        Assert.Equal("error", AgentLayerTestHarness.Tags(session)[TelemetryTags.RecoveryOutcome].GetString());
        server.ThrowIfFaulted();
    }

    [Theory]
    [InlineData("builtin", false, 1, "global", 0)]
    [InlineData("builtin", true, 1, "no_token", 1)]
    [InlineData("github", false, 0, "skipped_github", 0)]
    public async Task StandaloneStartup_DoesNotAuthenticateForRouting_AndPublishesAuthFailure(
        string provider, bool authFails, int acquisitions, string expectedBoundary, int expectedCode)
    {
        var cacheReads = 0;
        var launch = AgentTelemetryHost.ResolveLaunch(true, _ => null, enabled =>
        {
            cacheReads++;
            return new TelemetryInvocationContext(Guid.NewGuid().ToString("N"), enabled);
        });
        await using var harness = new AgentLayerTestHarness(context: launch.Context);
        var scope = harness.CreateScope();
        var auth = new AgentOfflineAuth
        {
            Acquire = _ => authFails
                ? throw new InvalidOperationException("offline signed-out account")
                : Task.FromResult(AgentOfflineAuth.Token()),
        };
        var code = await AgentModeEntry.RunSessionAsync(() => scope, async (session, _) =>
        {
            var startup = await AgentStartupBoundary.ResolveAsync(new AgentOptions { Provider = provider },
                auth, new AgentOfflineProfiles(), _ => new StubBoundary(), session, CancellationToken.None);
            session?.SetTag(TelemetryTags.RecoveryDataBoundary, startup.Boundary.Value);
            startup.Failure?.Throw();
            if (startup.Auth is not null)
                Assert.Equal(startup.Auth.AccessToken,
                    BuiltinProviderConfig.Build(new BuiltinProviderOptions(), startup.Auth.AccessToken).BearerToken);
        }, () => { });

        Assert.Equal(expectedCode, code);
        Assert.Equal(acquisitions, auth.AcquireCount);
        Assert.Equal(1, cacheReads);
        var entry = Assert.Single(harness.ReadEntries());
        var tags = AgentLayerTestHarness.Tags(entry);
        Assert.Equal(expectedBoundary, tags[TelemetryTags.RecoveryDataBoundary].GetString());
        Assert.Equal("Global", entry.GetProperty("DataBoundary").GetString());
    }

    private sealed class StubBoundary : IDataBoundaryResolver
    {
        public Task<DataBoundaryResult> ResolveAsync(string accessToken, CancellationToken ct = default) =>
            Task.FromResult(DataBoundaryResult.FromArm("Global", "offline-tenant"));
        public void Dispose() { }
    }

    [Fact]
    public async Task UserCancellationCallbacks_ArePartOfTheSameDrainBudget()
    {
        var clock = new AgentTestTimeProvider();
        await using var harness = new AgentLayerTestHarness(timeProvider: clock);
        var scope = harness.CreateScope();
        using var rootScope = scope.Telemetry.StartIndependentRootActivity("Agent.Session");
        var root = rootScope?.Activity;
        var lifetime = new AgentRuntimeLifetime(scope);
        using var userCancellation = new CancellationTokenSource();
        using var release = new ManualResetEventSlim();
        var entered = Signal();
        using var registration = userCancellation.Token.Register(() =>
        {
            entered.TrySetResult();
            release.Wait();
        });
        var cancelling = lifetime.CancelAsync(userCancellation);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var completion = lifetime.CompleteAsync(root,
            new Dictionary<string, object?> { [TelemetryTags.RecoveryOutcome] = "cancelled" }, ActivityStatusCode.Unset);
        try
        {
            clock.Advance(TimeSpan.FromSeconds(10));
            Assert.Equal(TelemetryLayerCompletion.PublishedAtDeadline,
                await completion.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.False(cancelling.IsCompleted);
            Assert.Equal("cancelled",
                AgentLayerTestHarness.Tags(Assert.Single(harness.ReadEntries()))[TelemetryTags.RecoveryOutcome].GetString());
        }
        finally
        {
            release.Set();
            await cancelling.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Fact]
    public async Task DiagnosticBoundary_UsesInjectedTelemetry_WithoutChangingRouting()
    {
        await using var harness = new AgentLayerTestHarness(context:
            new TelemetryInvocationContext(Guid.NewGuid().ToString("N"), true, DataBoundary: "Global"));
        var scope = harness.CreateScope();
        using var rootScope = scope.Telemetry.StartIndependentRootActivity("Agent.Session");
        var root = rootScope?.Activity;
        var profiles = new AgentOfflineProfiles
        {
            Telemetry = scope.Telemetry,
            Read = _ => Task.FromResult<Profile?>(new Profile { Name = "offline", TenantId = "offline-tenant" }),
        };
        using var http = new HttpClient(new AgentOfflineHttp((_, _) => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("""{"name":"offline-tenant","properties":{"dataBoundary":"EU"}}"""),
        })));
        using var resolver = new Core.Agent.Providers.DataBoundaryResolver(profiles, http, scope.Telemetry);
        Assert.Equal("eu", (await resolver.ResolveAsync("offline-token")).Value);
        Assert.Equal(TelemetryLayerCompletion.Published, await scope.CompleteAsync(root));
        var entry = Assert.Single(harness.ReadEntries(), entry =>
            entry.GetProperty("OperationName").GetString() == "Agent.DataBoundary");
        Assert.Equal("Global", entry.GetProperty("DataBoundary").GetString());
        Assert.Equal("eu", AgentLayerTestHarness.Tags(entry)[TelemetryTags.RecoveryDataBoundary].GetString());
    }

    private sealed class SilentRenderer : IAgentRenderer
    {
        public void OnText(string chunk) { }
        public void OnEvent(AgentEvent ev) { }
        public void OnSpinnerFrame(string frame, string verb) { }
        public void OnSpinnerStop() { }
    }
}
