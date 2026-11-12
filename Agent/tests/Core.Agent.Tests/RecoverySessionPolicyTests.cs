// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Recovery;
using Core.Agent.AgentMode;
using Core.Agent.Hosting;
using Core.Agent.Telemetry;
using Core.Agent.Tools;
using Core.Agent.Tools.Help;
using Core.Agent.Tools.Resource.Cache;
using Core.Agent.Tools.Resource.Sku;
using GitHub.Copilot;
using GitHub.Copilot.Rpc;
using Microsoft.Extensions.Time.Testing;
using Tests.Common.Stubs;
using Xunit;
using Tests.Common.Utilities;

namespace Core.Agent.Tests;

#pragma warning disable GHCP001 // Exercise the same preview permission contract used by ChatAgent.
[Collection("Console")]
public class RecoverySessionPolicyTests
{
    [Theory]
    [InlineData("oneshot")]
    [InlineData("interactive")]
    public void SessionConfig_ExposesTheSameToolsForBothTriggers(string style)
    {
        using var http = new HttpClient();
        var automatic = CreateConfig(RecoveryTrigger.Automatic, style, new(), http, CancellationToken.None);
        var manual = CreateConfig(RecoveryTrigger.Manual, style, new(), http, CancellationToken.None);

        Assert.Null(automatic.AvailableTools);
        Assert.Null(manual.AvailableTools);
        Assert.Equal(automatic.Tools!.Select(t => t.Name), manual.Tools!.Select(t => t.Name));
        Assert.Contains(manual.Tools!, t => t.Name == "get_command");
        Assert.Contains(manual.Tools!, t => t.Name == "find_resources");
        Assert.Contains(manual.Tools!, t => t.Name == "propose_plan");
        Assert.Contains(manual.Tools!, t => t.Name == "submit_response");
    }

    [Theory]
    [InlineData(RecoveryTrigger.Automatic, "oneshot")]
    [InlineData(RecoveryTrigger.Manual, "oneshot")]
    [InlineData(RecoveryTrigger.Automatic, "interactive")]
    [InlineData(RecoveryTrigger.Manual, "interactive")]
    public async Task SessionConfig_UsesExistingReadOnlyAndUserApprovalPolicy(RecoveryTrigger trigger, string style)
    {
        using var http = new HttpClient();
        using var timeout = new CancellationTokenSource(AgentTestTimeouts.HangGuard);
        var approval = new ConsoleApprovalPrompt();
        var config = CreateConfig(trigger, style, approval, http, timeout.Token);
        var context = new PermissionInvocation { SessionId = "offline-policy-test" };
        var read = await config.OnPermissionRequest!(new PermissionRequestCustomTool
        {
            ToolName = "find_resources",
            ToolDescription = "Read-only fixture",
        }, context);
        Assert.Equal(JsonSerializer.Serialize(PermissionDecision.ApproveOnce()), JsonSerializer.Serialize(read));

        var previousInput = Console.In;
        var previousOutput = Console.Out;
        Console.SetIn(new StringReader("n" + Environment.NewLine));
        Console.SetOut(TextWriter.Null);
        try
        {
            var pending = config.OnPermissionRequest(new PermissionRequestShell
            {
                CanOfferSessionApproval = false,
                Commands = [],
                FullCommandText = "echo offline",
                HasWriteFileRedirection = false,
                Intention = "Test approval without executing a command",
                PossiblePaths = [],
                PossibleUrls = [],
            }, context);
            Assert.False(pending.IsCompleted);
            var answer = await approval.PumpOneAsync(timeout.Token);
            Assert.True(answer.Handled);
            Assert.False(answer.Approved);
            Assert.Equal(JsonSerializer.Serialize(PermissionDecision.Reject(feedback: null)),
                JsonSerializer.Serialize(await pending));
        }
        finally
        {
            Console.SetIn(previousInput);
            Console.SetOut(previousOutput);
        }
    }

    // #1295 test 4b: an Esc that arrives after the turn completed is not an abort.
    [Fact]
    public async Task LateEsc_AfterTurnCompleted_DoesNotReportAbort()
    {
        var keys = new ScriptedConsoleKeys();
        keys.Push(ScriptedConsoleKeys.Esc);
        var agent = new FakeTurnAgent();
        agent.Send.SetResult();
        var reader = new LockingBlockingReader();
        using var console = new ConsoleSwap(reader);
        using var session = new CancellationTokenSource();
        try
        {
            var watcher = TurnPump.Watcher(agent, keys, new FakeTimeProvider());

            var result = await TurnPump.RunAsync(agent, new ConsoleApprovalPrompt(), watcher, interactive: false,
                () => session.IsCancellationRequested, session.Token).WaitAsync(TimeSpan.FromSeconds(60));

            Assert.False(result.UserAborted);
            Assert.Equal(0, agent.AbortCalls);
        }
        finally
        {
            reader.Release.Set();
        }
    }

    // #1295 test 7: Ctrl+C read by the watcher runs the same handler as CancelKeyPress.
    [Fact]
    public async Task WatcherCtrlC_RunsSameHandler_FlagThenForcedExit()
    {
        var keys = new ScriptedConsoleKeys();
        keys.Push(ScriptedConsoleKeys.CtrlC);
        var time = new SignalingFakeTimeProvider();
        using var session = new CancellationTokenSource();
        var ctrlC = new CtrlCController(session, time);
        var agent = new FakeTurnAgent();
        var watcher = TurnPump.Watcher(agent, keys, time, ctrlC.OnCtrlC);

        Assert.True(await watcher.RunAsync(CancellationToken.None, () => true).WaitAsync(TimeSpan.FromSeconds(60)));
        Assert.True(ctrlC.SessionCancelled);
        await time.WaitForTimerAsync(CtrlCController.ForcedExitDelay, TimeSpan.FromSeconds(60));
        time.Advance(CtrlCController.ForcedExitDelay);

        Assert.Equal(130, await ctrlC.ForcedExit.WaitAsync(TimeSpan.FromSeconds(60)));
        agent.Send.SetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => AgentModeEntry.AwaitTurnAsync(
            agent.Send.Task, () => null, redirected: false, userAborted: false, interactive: true,
            () => ctrlC.SessionCancelled).WaitAsync(TimeSpan.FromSeconds(60)));
    }

    // #1295 test 8a: Ctrl+C whose cancel has not landed yet still keeps the next '> ' prompt closed.
    [Fact]
    public async Task CtrlC_AfterLoopCheck_BeforeNextPrompt_DoesNotOpenPrompt()
    {
        using var session = new CancellationTokenSource();
        var cancelHeld = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var ctrlC = new CtrlCController(session, new FakeTimeProvider(), () => cancelHeld.Task);
        var reader = new LockingBlockingReader();
        using var console = new ConsoleSwap(reader);
        try
        {
            ctrlC.OnCtrlC();
            Assert.False(session.IsCancellationRequested);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                AgentModeEntry.ReadPromptLineAsync(() => ctrlC.SessionCancelled, session.Token)
                    .WaitAsync(TimeSpan.FromSeconds(60)));
            Assert.False(reader.Entered.IsSet);
        }
        finally
        {
            cancelHeld.TrySetResult();
            reader.Release.Set();
        }
    }

    private static SessionConfig CreateConfig(
        RecoveryTrigger trigger, string style, ConsoleApprovalPrompt approval, HttpClient http, CancellationToken ct)
    {
        var options = new AgentOptions();
        options.Recovery.Style = style;
        var settings = RecoverySessionSettings.Resolve(options, trigger);
        var auth = new StubAuthService();
        var profiles = new StubProfileService(seedDefault: false);
        options.Provider = "github";
        return ChatAgent.BuildSessionConfigAsync(
            options, auth, "offline test", new CachedResourceTools(new ArmDetailsClient(auth), profiles),
            new HelpTools("unused-az", new AgentTestConfiguration()), new AvailableLocationsTool(auth, profiles, http),
            new SubmitResponseTool(), new ProposePlanTool(), "unused-az", profiles, approval, new EventRenderer(),
            new SessionState { IsOneShot = !settings.IsInteractive }, null,
            settings.IsInteractive ? "inline" : "oneshot",
            new AgentInvocationContext(session: null, new StubTelemetryService(), lifetime: null, lifetimeToken: ct),
            ct).GetAwaiter().GetResult();
    }
}
