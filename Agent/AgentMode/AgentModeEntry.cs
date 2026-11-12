using System.Diagnostics;
using Core.Abstractions;
using Core.Abstractions.Recovery;
using Core.Agent;
using Core.Agent.Hosting;
using Core.Agent.Plan;
using Core.Agent.Telemetry;
using Core.DependencyInjection;
using Core.Runtime;
using Core.Telemetry;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Agent.AgentMode;

/// <summary>
/// Standalone agent-process entry point (azclips-agent.exe) — spawned by the
/// classic CLI when an invocation fails. Owns the chat loop, banner, and stdin
/// so the approval pump and prompt share a single reader.
/// </summary>
public static class AgentModeEntry
{
    private const string FirstPartyApplicationId = "04b07795-8ddb-461a-bbee-02f9e1bf7b46";

    // Session-level interaction brake (MVP constants, not configurable yet):
    // counts user<->agent messages (each new user message +1, internal
    // self-correction re-sends excluded), does NOT reset, soft reminder at
    // SoftTurns, one hard stop at HardTurns.
    private const int EscapeValveSoftTurns = 12;
    private const int EscapeValveHardTurns = 20;
    private static readonly string[] SessionResultTags =
    [
        TelemetryTags.RecoverySessionStyle, TelemetryTags.RecoveryProvider,
        TelemetryTags.AgentModel, TelemetryTags.AgentModelSource, TelemetryTags.AgentTokenElapsedMs,
    ];

    /// <summary>
    /// Records the effective Agent settings before any startup step that can fail.
    /// New config keys go into <see cref="AgentOptions"/>; new per-run overrides go into
    /// <see cref="RecoverySessionSettings.Resolve"/>. Both are serialized whole, so new
    /// fields are recorded without changes here.
    /// </summary>
    internal static void NoteAgentSettings(
        TraceAgentRenderer? trace, AgentOptions options, RecoverySessionSettings run) =>
        trace?.Note("agent_settings", new { options, run });

    public static async Task<int> RunAsync(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        using var cts = new CancellationTokenSource();
        AgentRuntimeLifetime? activeLifetime = null;
        SpinnerService? activeSpinner = null;
        var ctrlC = new CtrlCController(cts, TimeProvider.System, CancelAsync);
        ConsoleCancelEventHandler cancelHandler = (_, e) => { e.Cancel = true; ctrlC.OnCtrlC(); };
        Console.CancelKeyPress += cancelHandler;

        var failedCommand = string.Empty;
        var errorText     = string.Empty;

        // Runtime services still initialize as CLI, but all Agent-process telemetry borrows
        // the isolated layer pipeline. The scope, never DI, owns provider/file completion.
        AzclipsServiceProvider.OverrideRegistration(services =>
        {
            services.AddSingleton<TelemetryLayerScopeFactory>();
            services.AddSingleton<AgentDataCleanup>();
            services.AddSingleton(sp => new AgentTelemetryHost(
                sp.GetRequiredService<TelemetryLayerScopeFactory>(),
                sp.GetRequiredService<IConfigurationService>(),
                sp.GetRequiredService<DataBoundaryResolver>()));
            services.AddSingleton<ITelemetryProvider>(sp => new AgentTelemetryProvider(
                sp.GetRequiredService<AgentTelemetryHost>().Scope.TelemetryProvider));
            services.AddSingleton<ITelemetryService>(sp => new BorrowedAgentTelemetryService(
                sp.GetRequiredService<AgentTelemetryHost>().Scope.Telemetry));
        });
        AzclipsServiceProvider.Initialize(
            new ClientMetadata(ClientType.CLI, FirstPartyApplicationId));

        TraceAgentRenderer? trace = null;
        return await RunSessionCoreAsync(
            async () => await AzclipsServiceProvider.GetRequiredService<AgentTelemetryHost>().GetScopeAsync(),
            RunAgentAsync,
            () =>
            {
                Console.CancelKeyPress -= cancelHandler;
                trace?.Dispose();
                AzclipsServiceProvider.Dispose();
            },
            ctrlC: ctrlC,
            restoreConsole: () => RestoreConsole(Volatile.Read(ref activeSpinner), ShowCursor));

        async Task RunAgentAsync(Activity? session, AgentRuntimeLifetime lifetime)
        {
            Volatile.Write(ref activeLifetime, lifetime);
            var request = await RecoveryInput.ReadAsync(args, cts.Token);
            if (request is not null)
            {
                failedCommand = request.FailedCommand;
                errorText = request.ErrorText;
            }

            var auth        = AzclipsServiceProvider.GetRequiredService<IAuthService>();
            var profileService = AzclipsServiceProvider.GetRequiredService<IProfileService>();
            var config      = AzclipsServiceProvider.GetRequiredService<IConfigurationService>();
            await RetiredAgentKeys.TryRemoveAsync(config, cts.Token);
            var agentOpts   = config.GetConfiguration<AgentOptions>();
            RecoveryTelemetry.RecordConfiguration(session, agentOpts);
            AzclipsServiceProvider.GetRequiredService<AgentDataCleanup>().StartInBackground(agentOpts.Trace.Path);
            if (!agentOpts.Recovery.IsEnabled)
                throw new InvalidOperationException("AI recovery is disabled. Set agent.recovery.trigger to manual or auto before troubleshooting.");
            var recoverySettings = RecoverySessionSettings.Resolve(
                agentOpts, request?.Trigger ?? RecoveryTrigger.Automatic, request?.SessionStyle);
            if (recoverySettings.IsInteractive && (Console.IsInputRedirected || Console.IsOutputRedirected))
                throw new InvalidOperationException("Interactive recovery requires a terminal. Use --style oneshot when input or output is redirected.");
            session?.SetTag(TelemetryTags.RecoverySessionStyle, recoverySettings.SessionStyle);
            session?.SetTag(TelemetryTags.RecoveryTrigger,
                recoverySettings.Trigger == RecoveryTrigger.Manual ? "manual" : "automatic");
            // Prompt is chosen by interaction mode: oneshot → oneshot.md,
            // interactive → inline.md.
            var entrypoint  = recoverySettings.IsInteractive ? "inline" : "oneshot";
            // Quiet mode in one-shot: suppress streaming prose and agent-internal
            // event spam; the user sees only the spinner + the final AI help.
            var quiet = !recoverySettings.IsInteractive;

            var flavor   = Environment.GetEnvironmentVariable("AZURECLI_FLAVOR") ?? AgentFlavor.AzureCli;
            var az     = FindAzExe() ?? "az";
            var consoleRenderer = new ConsoleAgentRenderer(quiet, agentOpts.Interactive.Verbose);
            // When agent.trace.enabled, wrap the console renderer to record a JSONL
            // trace under agent.trace.path. Prose suppression after submit_response
            // lives on ConsoleAgentRenderer itself (always-on), independent of trace.
            IAgentRenderer renderer = consoleRenderer;
            if (agentOpts.Trace.Enabled && !string.IsNullOrWhiteSpace(agentOpts.Trace.Path))
            {
                // Expand a leading ~ in a user-set path — .NET
                // does NOT expand it, so without this the trace lands in a literal "~"
                // directory. Resolve to an absolute path so the announced location works.
                var traceDir = ExpandHome(agentOpts.Trace.Path);
                var fileName = $"trace-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.jsonl";
                var fullPath = Path.GetFullPath(Path.Combine(traceDir, fileName));
                trace = new TraceAgentRenderer(consoleRenderer, fullPath);
                renderer = trace;
                // Always announce the trace file (even in quiet) — if it's on, the
                // user explicitly wants it and needs to know where to look.
                Console.WriteLine($"   trace: {fullPath}");
            }
            NoteAgentSettings(trace, agentOpts, recoverySettings);
            // await using: the spinner is Started before CreateAsync (below), so it
            // must be Stopped even when CreateAsync throws or the user Ctrl+Cs during
            // cold start. Disposal runs on the way out of this scope — before the
            // catch blocks — so the spinner line is cleared and the cursor restored
            // before the error is printed.
            await using var spinner = new SpinnerService(renderer);
            Volatile.Write(ref activeSpinner, spinner);

            // One watcher for all turns; SendAndPumpAsync runs it and stops it before
            // any approval reads stdin. The abort callback closes over agentRef so we
            // can wire the watcher before the agent exists.
            ChatAgent? agentRef = null;
            var watcher  = new AbortKeyWatcher(
                onAbort: ct => agentRef!.AbortAsync(ct),
                onQuit: ctrlC.OnCtrlC);
            var approval = new ConsoleApprovalPrompt(spinner, renderer);

            // Funnel Azclips-internal logs through the renderer so they don't
            // race the spinner. Verbose/Debug/Info dropped; Warn/Error become
            // HostLogEmitted events the renderer draws under its lock.
            using var _logScope = ScopedLoggingService.UseScope(new RendererLoggingService(renderer));

            // Start the spinner BEFORE CreateAsync: daemon cold-start + silent auth
            // take several seconds, and without this the screen hangs blank. The
            // first chat turn re-Starts the spinner with the same verb (Start is
            // idempotent — it Stops first), so this seams into the turn cleanly.
            // It also has to precede the token acquisition below, which is the other
            // thing that can spend seconds here.
            spinner.Start("Analyzing error root cause using AI");

            // Reuse one Azure token for the builtin provider and the boundary lookup.
            // The boundary also selects the model endpoint (see BuiltinEndpointResolver).
            // Do not add a second concurrent MSAL acquisition over the shared token cache.
            var startup = await AgentStartupBoundary.ResolveAsync(
                agentOpts, auth, profileService, handedOverBoundary: null,
                (tenant, token, ct) => AgentStartupBoundary.LookupDataBoundary(
                    AzclipsServiceProvider.GetRequiredService<DataBoundaryResolver>(), tenant, token, ct),
                session, cts.Token, request?.ModelProfileName);

            // Telemetry retains the launch upload region; this lookup only selects the model endpoint.
            trace?.Note("data_boundary", new { boundary = startup.DataBoundary });

            var azureAuth = startup.Auth;
            // Preserves today's behavior exactly: a signed-out user still gets
            // BuildAuthErrorMessage()'s actionable "run az login", with its original stack, and
            // recovery still stops here — only now the outcome was recorded first.
            startup.Failure?.Throw();

            var agent = await ChatAgent.CreateAsync(
                flavor: flavor,
                auth: auth,
                profileService: profileService,
                configurationService: config,
                boundaryResolver: AzclipsServiceProvider.GetRequiredService<DataBoundaryResolver>(),
                azExePath: az,
                approvalSink: approval,
                renderer: renderer,
                options: agentOpts,
                entrypoint: entrypoint,
                lifetime: lifetime,
                spinner: spinner,
                failedCommand: failedCommand,
                preAcquiredAuth: azureAuth,
                recoverySettings: recoverySettings,
                ct: cts.Token,
                dataBoundary: startup.DataBoundary);
            agent.RecordModel(session);
            agentRef = agent;
            // Always-on prose suppression: after the final outcome renders,
            // ConsoleAgentRenderer drops any trailing prose the model streams past
            // the terminator — independent of whether tracing is on.
            agent.SetOnFinalRendered(consoleRenderer.MarkFinalRendered);
            // Record the plan exactly as shown, so evaluations grade what the user saw.
            agent.SetOnOutcomeShown(steps => trace?.Note("plan_shown", new
            {
                steps = steps?.Select(step => new { id = step.Id, label = step.Label, cmd = step.Cmd }).ToArray(),
            }));
            trace?.Note("session_start", new
            {
                entrypoint,
                flavor,
                sessionStyle = recoverySettings.SessionStyle,
                endpointHost = agent.Endpoint?.Host,
                endpointSource = agent.Endpoint?.Source,
                dataBoundary = agent.Endpoint?.Boundary,
            });

            var firstPrompt = BuildFirstPrompt(failedCommand, errorText, flavor);

            var interactive = recoverySettings.IsInteractive;

            try
            {
                await RunChatLoopAsync(agent, approval, watcher, spinner, firstPrompt, interactive,
                    () => ctrlC.SessionCancelled, cts.Token);
                // Classify the last turn at the shared exit for both interaction modes.
                ThrowIfSessionEndedInError(agent.LastError, cts.Token, ctrlC.Requested);
                if (!agent.SubmitRendered && !agent.HasPendingPlan)
                    throw new InvalidOperationException("AI diagnosis ended without a recovery plan or final response.");
            }
            finally
            {
                agent.RecordModel(session);
            }
        }

        Task CancelAsync() =>
            Volatile.Read(ref activeLifetime) is { } lifetime ? lifetime.CancelAsync(cts) : cts.CancelAsync();
    }

    private static void ShowCursor()
    {
        try { Console.CursorVisible = true; } catch { }
    }

    /// <summary>Stops spinner frames for good, then shows the cursor without taking the renderer lock.</summary>
    internal static void RestoreConsole(SpinnerService? spinner, Action showCursor)
    {
        try { spinner?.Quiesce(); } catch { }
        showCursor();
    }

    internal static Task<int> RunSessionAsync(
        Func<ITelemetryLayerScope> getScope,
        Func<Activity?, AgentRuntimeLifetime, Task> runAgent,
        Action disposeServices,
        TimeProvider? timeProvider = null) =>
        RunSessionCoreAsync(() => Task.FromResult(getScope()), runAgent, disposeServices, timeProvider);

    internal static Task<int> RunSessionAsync(
        Func<ITelemetryLayerScope> getScope,
        Func<Activity?, AgentRuntimeLifetime, Task> runAgent,
        Action disposeServices,
        TimeProvider? timeProvider,
        CtrlCController? ctrlC,
        Action? restoreConsole) =>
        RunSessionCoreAsync(() => Task.FromResult(getScope()), runAgent, disposeServices, timeProvider, ctrlC, restoreConsole);

    /// <remarks>
    /// Races the session against the Ctrl+C forced exit, as System.CommandLine's InvocationPipeline
    /// does: when the forced exit wins, return 130 and do not await the abandoned session.
    /// </remarks>
    private static async Task<int> RunSessionCoreAsync(
        Func<Task<ITelemetryLayerScope>> getScope,
        Func<Activity?, AgentRuntimeLifetime, Task> runAgent,
        Action disposeServices,
        TimeProvider? timeProvider = null,
        CtrlCController? ctrlC = null,
        Action? restoreConsole = null)
    {
        try
        {
            var session = RunSessionToEndAsync(getScope, runAgent, disposeServices, timeProvider, ctrlC);
            if (ctrlC is null) return await session.ConfigureAwait(false);
            var first = await Task.WhenAny(session, ctrlC.ForcedExit).ConfigureAwait(false);
            return await ((Task<int>)first).ConfigureAwait(false);
        }
        finally
        {
            try { restoreConsole?.Invoke(); }
            catch { Console.Error.WriteLine("Agent telemetry: console_restore_failed"); }
        }
    }

    private static async Task<int> RunSessionToEndAsync(
        Func<Task<ITelemetryLayerScope>> getScope,
        Func<Activity?, AgentRuntimeLifetime, Task> runAgent,
        Action disposeServices,
        TimeProvider? timeProvider,
        CtrlCController? ctrlC)
    {
        Activity? session = null;
        TelemetryActivityScope? sessionScope = null;
        ITelemetryLayerScope? scope = null;
        AgentRuntimeLifetime? lifetime = null;
        // Leave unclassified exits observable instead of reporting success.
        var outcome = RecoveryOutcome.Unknown;
        try
        {
            try
            {
                // Resolve here so initialization failures use the normal error path.
                // Resolution attaches the listener before Agent.Session starts.
                scope = await getScope();
                lifetime = new AgentRuntimeLifetime(scope, timeProvider);
                sessionScope = scope.Context.TryGetRecoveryParentContext(out var recoveryParent)
                    ? scope.Telemetry.StartActivityWithParent("Agent.Session", recoveryParent)
                    : scope.Telemetry.StartIndependentRootActivity("Agent.Session");
                session = sessionScope?.Activity;
                session?.SetTag(TelemetryTags.ProcessName, "azclips-agent");

                await runAgent(session, lifetime);
                // A Ctrl+C whose handler started before this point ends the session as cancelled.
                if (ctrlC?.Requested == true)
                {
                    outcome = RecoveryOutcome.Cancelled;
                    return CtrlCController.CancelledExitCode;
                }
                outcome = RecoveryOutcome.Ok;
                return 0;
            }
            catch (Exception) when (ctrlC?.Requested == true)
            {
                outcome = RecoveryOutcome.Cancelled;
                return CtrlCController.CancelledExitCode;
            }
            catch (AgentSessionFailedException ex)
            {
                // The renderer already reported this error.
                // Record its classification only; the message may contain user data.
                outcome = RecoveryOutcome.Error(ClassifySdkFailure(ex.ErrorType));
                return 1;
            }
            catch (OperationCanceledException)
            {
                outcome = RecoveryOutcome.Cancelled;
                return 130;
            }
            catch (Exception ex)
            {

                outcome = RecoveryOutcome.Error(RecoveryFailure.HostFault);
                WriteColor(ConsoleColor.Red, $"agent mode error: {ex.Message}");
                return 1;
            }
        }
        finally
        {
            if (lifetime is not null)
            {
                // Only the host-owned result fields are frozen here. Auth/cache callbacks may
                // still be active; do not enumerate their mutable root tags during drain admission.
                var tags = new Dictionary<string, object?>();
                foreach (var key in SessionResultTags)
                {
                    if (session?.GetTagItem(key) is { } value)
                        tags[key] = value;
                }
                tags[TelemetryTags.RecoveryOutcome] = outcome.Value;
                if (outcome.FailureReason is { } reason)
                    tags[TelemetryTags.RecoveryFailureReason] = reason;
                try
                {
                    await lifetime.CompleteAsync(session, tags, outcome.Status).ConfigureAwait(false);
                    await scope!.DisposeAsync().ConfigureAwait(false);
                }
                catch
                {
                    Console.Error.WriteLine("Agent telemetry: completion_failed");
                }
            }
            // Keep the handle through completion, then restore ambient in this caller flow.
            // The layer has already claimed the terminal record before this owner disposes it.
            try { sessionScope?.Dispose(); }
            catch { Console.Error.WriteLine("Agent telemetry: session_disposal_failed"); }
            try { disposeServices(); }
            catch { Console.Error.WriteLine("Agent telemetry: service_disposal_failed"); }
        }
    }

    /// <summary>Maps SDK error types to the closed recovery failure vocabulary.</summary>
    /// <remarks>
    /// Do not export raw SDK values; map unlisted values to <c>sdk_other</c>.
    /// </remarks>
    internal static RecoveryFailure ClassifySdkFailure(string? errorType) => errorType switch
    {
        // EventRenderer uses "unknown" when the SDK omits the error type.
        null or "unknown" => RecoveryFailure.Unknown,
        "rate_limit"      => RecoveryFailure.RateLimit,
        "authentication"  => RecoveryFailure.Authentication,
        _                 => RecoveryFailure.SdkOther,
    };

    /// <summary>
    /// Binds the diagnostic-only probe to this host's telemetry service.
    /// </summary>
    private static async Task RunChatLoopAsync(
        ChatAgent agent,
        ConsoleApprovalPrompt approval,
        AbortKeyWatcher watcher,
        SpinnerService spinner,
        string firstPrompt,
        bool interactive,
        Func<bool> sessionCancelled,
        CancellationToken ct)
    {
        // Session-level interaction brake: counts user<->agent messages and never
        // resets. The first prompt is message 1; each new "> " line adds one.
        // Internal self-correction re-sends inside SendWithToolEnforcementAsync are
        // not counted (they aren't new user input).
        int userMessages = 1;
        // The very first turn is the error-recovery opener — show a spinner verb
        // that says so. Later turns (and any redirect) fall back to "thinking".
        var nextPrompt = await SendWithToolEnforcementAsync(agent, approval, watcher, spinner, firstPrompt,
            interactive, sessionCancelled, ct, initialVerb: "Analyzing error root cause using AI");

        // oneshot closes on its first valid plan (rendered by the plan hook) or on
        // submit_response; drain any redirect until it does (or the agent stops
        // without one, e.g. an out-of-scope rejection), but never open a "> " prompt.
        if (!interactive)
        {
            while (!agent.SessionClosed && nextPrompt is not null && !sessionCancelled())
                nextPrompt = await SendWithToolEnforcementAsync(agent, approval, watcher, spinner, nextPrompt,
                    interactive, sessionCancelled, ct);

            // Fallback render: if a plan is still pending (nothing rendered it), show
            // it here so one-shot never exits on a blank screen.
            if (!agent.SubmitRendered && agent.HasPendingPlan)
                approval.ShowPlan(new ConsoleApprovalPrompt.PlanContent(
                    agent.PendingIssues, agent.PendingPlanSteps, agent.PendingNeeds, agent.PendingDefaults));

            return;
        }

        while (!sessionCancelled())
        {
            // The session ends when the agent submits its final response.
            if (agent.SessionClosed) break;

            string line;
            if (nextPrompt is not null)
            {
                // User typed free text at the y/n prompt — replay it as the next
                // chat turn. This is user input, so it counts toward the brake.
                line = nextPrompt;
                nextPrompt = null;
                userMessages++;
            }
            else
            {
                var read = await ReadPromptLineAsync(sessionCancelled, ct).ConfigureAwait(false);
                if (read is null) break;
                line = read.Trim();
                if (line.Length == 0) continue;
                if (line is "exit" or "quit" or "/exit" or "/quit" or ":q") break;
                userMessages++;
            }

            if (userMessages == EscapeValveSoftTurns)
                WriteColor(ConsoleColor.DarkGray,
                    $"  this session has run {userMessages} exchanges. Type 'exit' or Ctrl+C to stop.");
            else if (userMessages >= EscapeValveHardTurns)
            {
                WriteColor(ConsoleColor.Yellow,
                    $"  ending the session after {userMessages} exchanges — start a fresh one if you still need help.");
                await agent.AbortAsync(ct).ConfigureAwait(false);
                break;
            }

            nextPrompt = await SendWithToolEnforcementAsync(agent, approval, watcher, spinner, line,
                interactive, sessionCancelled, ct);
        }
    }

    /// <summary>Opens the "> " prompt and reads one line; null at end of input.</summary>
    /// <exception cref="OperationCanceledException">The session was cancelled before or during the read.</exception>
    internal static Task<string?> ReadPromptLineAsync(Func<bool> sessionCancelled, CancellationToken ct)
    {
        if (sessionCancelled()) throw new OperationCanceledException(ct);
        Console.WriteLine();
        Console.Write("> ");
        return ConsoleLineInput.ReadLineAsync(ct);
    }

    /// <summary>Reports a final-turn SDK failure or caller cancellation after the chat loop.</summary>
    /// <remarks>
    /// Call once after the loop; it may return after catching a failed send.
    /// Pass <see cref="ChatAgent.LastError"/>, not <see cref="ChatAgent.StartupError"/>.
    /// Earlier failures do not override a successful final turn.
    /// Ctrl+C (<paramref name="ctrlCRequested"/> or a cancelled <paramref name="ct"/>) takes
    /// precedence over a terminal SDK error, so a session the user cancelled exits as cancelled.
    /// </remarks>
    internal static void ThrowIfSessionEndedInError(
        SessionErrored? terminalError, CancellationToken ct, bool ctrlCRequested = false)
    {
        if (ctrlCRequested) throw new OperationCanceledException(ct);
        ct.ThrowIfCancellationRequested();

        if (terminalError is { } err)
        {
            throw new AgentSessionFailedException(err.ErrorType, err.Message);
        }
    }

    // Max times we re-prompt a bare-prose turn before giving up and letting it
    // through (avoid an infinite correction loop if the model keeps disobeying).
    private const int MaxToolEnforcementRetries = 3;

    // Wraps SendAndPumpAsync with the plan-protocol guard: if a turn produced
    // only prose (no tool call, no submit_response), the model broke the "always
    // act through tools" contract — re-prompt it to call propose_plan/submit_response.
    // The SDK gives no way to forbid bare text up front, so this is the host-side
    // backstop. Returns the same redirect signal SendAndPumpAsync does.
    // INTERIM: the durable fix is forcing tool use up front (tool_choice="required"
    // or grammar-constrained generation); that UI/constraint direction is being
    // tried in a separate worktree. This after-the-fact correction is the stopgap.
    private static async Task<string?> SendWithToolEnforcementAsync(
        ChatAgent agent,
        ConsoleApprovalPrompt approval,
        AbortKeyWatcher watcher,
        SpinnerService spinner,
        string prompt,
        bool interactive,
        Func<bool> sessionCancelled,
        CancellationToken ct,
        string initialVerb = "thinking")
    {
        using var turn = agent.Telemetry.StartActivity("Agent.Turn");
        var (redirect, userAborted) = await SendAndPumpAsync(
            agent, approval, watcher, spinner, prompt, interactive, sessionCancelled, ct, initialVerb);

        for (int attempt = 0; attempt < MaxToolEnforcementRetries; attempt++)
        {
            // The user aborted this turn with Esc; do not re-prompt it.
            if (userAborted) break;
            // A redirect means the user steered the turn — respect it, don't enforce.
            if (redirect is not null) break;
            // One-shot already showed its final outcome (a valid plan ends the session).
            if (agent.SessionClosed) break;
            // Compliant turn: it closed with propose_plan or submit_response.
            // (get_command/bash alone do NOT count — a query-then-prose turn is a violation.)
            if (agent.LastSendCalledClosingTool) break;
            // Errored turn: don't pile a correction on top of a failure.
            if (agent.LastError is not null) break;

            using var reprompt = agent.Telemetry.StartActivity("Agent.Reprompt");
            reprompt?.SetTag(TelemetryTags.RecoveryReprompts, attempt + 1);

            WriteColor(ConsoleColor.DarkGray, "  ↯ agent replied without acting — asking it to use a tool");
            const string correction =
                "You replied with prose instead of calling a tool. That is not allowed. " +
                "Call propose_plan to suggest a fix (the user must approve before any command runs), " +
                "or submit_response to end the session. Do it now.";
            (redirect, userAborted) = await SendAndPumpAsync(
                agent, approval, watcher, spinner, correction, interactive, sessionCancelled, ct);
        }

        return redirect;
    }

    /// <summary>Runs one turn and pumps its approvals.</summary>
    /// <returns>
    /// Redirect: the feedback the user typed at a y/n approval, replayed as the next turn.
    /// UserAborted: Esc ended only this turn (interactive mode).
    /// </returns>
    /// <remarks>
    /// Owns the key watcher. Before an approval reads stdin, the watcher is cancelled and awaited
    /// (PSReadLine signal-then-Join), then restarted after the read. If the watcher already ended the
    /// turn, the approval is rejected without a prompt (Codex: an aborted turn's approval is Abort).
    /// </remarks>
    internal static async Task<(string? Redirect, bool UserAborted)> SendAndPumpAsync(
        ITurnAgent agent,
        ConsoleApprovalPrompt approval,
        AbortKeyWatcher watcher,
        SpinnerService? spinner,
        string prompt,
        bool interactive,
        Func<bool> sessionCancelled,
        CancellationToken ct,
        string initialVerb = "thinking")
    {
        spinner?.Start(initialVerb);
        var sendTask = agent.SendAsync(prompt, ct);
        bool TurnRunning() => !sendTask.IsCompleted;

        string? redirect = null;
        var userAborted = false;
        CancellationTokenSource? watcherCts = null;
        Task<bool>? watcherTask = null;
        try
        {
            while (true)
            {
                watcherCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                watcherTask = watcher.RunAsync(watcherCts.Token, TurnRunning);

                using var pumpCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                var pumpTask = approval.DequeueAsync(pumpCts.Token);
                var completed = await Task.WhenAny(sendTask, pumpTask, watcherTask).ConfigureAwait(false);
                if (completed != pumpTask)
                {
                    pumpCts.Cancel();
                    if (await pumpTask.ConfigureAwait(false) is { } late)
                        ConsoleApprovalPrompt.Skip(late, sessionCancelled());
                    break;
                }

                var pending = await pumpTask.ConfigureAwait(false);
                if (pending is null) break;

                userAborted = await StopWatcherAsync(watcherCts, watcherTask).ConfigureAwait(false);
                watcherCts = null;
                watcherTask = null;
                if (sessionCancelled())
                {
                    ConsoleApprovalPrompt.Skip(pending, cancelled: true);
                    break;
                }
                if (userAborted)
                {
                    ConsoleApprovalPrompt.Skip(pending, cancelled: false);
                    break;
                }

                var outcome = await approval.AnswerAsync(pending, ct).ConfigureAwait(false);
                if (outcome.FeedbackPrompt is { } feedback)
                {
                    // Reject + redirect: abort the in-flight turn so the SDK
                    // stops calling tools, surface a transcript line, and hand
                    // the feedback back to the chat loop as the next prompt.
                    redirect = feedback;
                    await agent.AbortAsync(ct).ConfigureAwait(false);
                    WriteColor(ConsoleColor.DarkGray, $"  ↯ told to do differently: {Truncate(feedback, 60)}");
                    break;
                }
            }
        }
        finally
        {
            if (watcherCts is not null && watcherTask is not null)
                userAborted |= await StopWatcherAsync(watcherCts, watcherTask).ConfigureAwait(false);
            approval.SkipQueued(sessionCancelled());
            spinner?.Stop();
        }
        if (userAborted && !sessionCancelled()) WriteColor(ConsoleColor.DarkGray, "  ↯ aborted by user");

        var turnAborted = await AwaitTurnAsync(
            sendTask, () => agent.LastError, redirect is not null, userAborted, interactive, sessionCancelled)
            .ConfigureAwait(false);
        return (redirect, turnAborted);
    }

    private static async Task<bool> StopWatcherAsync(CancellationTokenSource watcherCts, Task<bool> watcherTask)
    {
        watcherCts.Cancel();
        try { return await watcherTask.ConfigureAwait(false); }
        catch { return false; }
        finally { watcherCts.Dispose(); }
    }

    /// <summary>Awaits a finished turn and decides whether its cancellation ends the session.</summary>
    /// <returns>True when Esc aborted only this turn and the session continues.</returns>
    /// <exception cref="OperationCanceledException">
    /// Ctrl+C, Esc in oneshot mode, or a cancellation nothing else explains.
    /// </exception>
    internal static async Task<bool> AwaitTurnAsync(
        Task sendTask, Func<SessionErrored?> lastError, bool redirected, bool userAborted, bool interactive,
        Func<bool> sessionCancelled)
    {
        try { await sendTask.ConfigureAwait(false); }
        catch (Exception ex) when (sessionCancelled())
        {
            // Ctrl+C ends the session as cancelled, even after an SDK error or a failed send.
            if (ex is OperationCanceledException) throw;
            throw new OperationCanceledException("Session cancelled by Ctrl+C.", ex);
        }
        catch (OperationCanceledException) when (lastError() is { } err)
        {
            // Allow another interactive turn; the final check still reads LastError.
            WriteColor(ConsoleColor.Red, $"  ✗ session.error [{err.ErrorType}] {err.Message}");
        }
        catch (OperationCanceledException) when (redirected)
        {
            // Abort caused by user redirect — already announced; swallow.
        }
        catch (OperationCanceledException) when (userAborted && interactive)
        {
            // Esc aborted only this turn — already announced; return to the prompt.
            return true;
        }

        if (sessionCancelled()) throw new OperationCanceledException("Session cancelled by Ctrl+C.");
        // Esc can land after the turn's last cancel check, so the send completes normally.
        if (!userAborted) return false;
        if (!interactive) throw new OperationCanceledException("Turn aborted by user.");
        return true;
    }

    // The CLI passes the failed command without the program name ("rest --method fetch
    // ..."). Show it to the model as the user typed it ("az rest ..."), or the model
    // tends to copy the bare form into plan steps that would not run. The PowerShell
    // flavor passes cmdlets, which have no prefix. internal for unit testing.
    internal static string BuildFirstPrompt(string failedCommand, string? errorText, string flavor)
    {
        var command = failedCommand.Trim();
        if (AgentFlavor.IsAzureCli(flavor) &&
            command.Length > 0 &&
            !PlanCommandNormalizer.StartsWithAz(command))
        {
            command = "az " + command;
        }

        return string.IsNullOrWhiteSpace(errorText)
            ? $"I tried `{command}` but it failed. Help me fix it."
            : $"I tried `{command}` but it failed:\n{errorText}\nHelp me fix it.";
    }

    // .NET does not expand a leading "~"; do it ourselves so a user value such as
    // ~/traces resolves under the user profile instead of a literal "~" dir.
    internal static string ExpandHome(string path)
    {
        if (path == "~")
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (path.StartsWith("~/", StringComparison.Ordinal) || path.StartsWith("~\\", StringComparison.Ordinal))
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[2..]);
        return path;
    }

    private static string? FindAzExe()
    {
        // Packaged layout: azclips-agent.exe sits next to az.exe in the CLI
        // publish dir (hybrid layout: launcher siblings share the runtime pack,
        // agent-only NuGet deps + prompts + copilot native live under
        // Extensions/Microsoft.Azure.Azclips.Extensions.Agent/). Dev-loop layout
        // still puts az.exe in <artifacts>/Core.Cli/ next to the agent's
        // <artifacts>/Core.Agent/; keep both fallbacks so a debug F5 works
        // without packaging.
        var baseDir = AppContext.BaseDirectory;
        var name = OperatingSystem.IsWindows() ? "az.exe" : "az";
        foreach (var candidate in new[]
        {
            Path.Combine(baseDir, name),
            Path.Combine(baseDir, "..", "Core.Cli", name),
        })
        {
            if (File.Exists(candidate)) return Path.GetFullPath(candidate);
        }
        return null;
    }

    private static void WriteColor(ConsoleColor color, string text)
    {
        var prev = Console.ForegroundColor;
        try
        {
            Console.ForegroundColor = color;
            Console.WriteLine(text);
        }
        finally
        {
            Console.ForegroundColor = prev;
        }
    }

    private static string Truncate(string s, int max) =>
        s.Length > max ? s[..max] + "…" : s;
}
