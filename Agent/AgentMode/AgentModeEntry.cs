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
        // Captured before anything can call Console.SetIn, so a replaced reader is detected.
        var originalInput = Console.In;
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        using var cts = new CancellationTokenSource();
        AgentRuntimeLifetime? activeLifetime = null;
        ConsoleCancelEventHandler cancelHandler = (_, e) => { e.Cancel = true; RequestCancellation(); };
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
        ConsoleKeyReader? keyReader = null;
        return await RunSessionCoreAsync(
            async () => await AzclipsServiceProvider.GetRequiredService<AgentTelemetryHost>().GetScopeAsync(),
            RunAgentAsync,
            () =>
            {
                Console.CancelKeyPress -= cancelHandler;
                trace?.Dispose();
                AzclipsServiceProvider.Dispose();
            },
            restoreTerminal: () => keyReader?.Dispose());

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
            RequireTerminal(recoverySettings.IsInteractive, Console.IsInputRedirected, Console.IsOutputRedirected);
            var interactive = recoverySettings.IsInteractive;
            // The key reader is the only stdin reader of an interactive session. It starts
            // before authentication so Ctrl+C and Esc reach the startup steps below.
            if (StartsKeyReader(interactive, Console.IsInputRedirected, Console.In, originalInput))
            {
                keyReader = new ConsoleKeyReader(new SystemConsoleKeySource());
                keyReader.Start();
            }
            session?.SetTag(TelemetryTags.RecoverySessionStyle, recoverySettings.SessionStyle);
            session?.SetTag(TelemetryTags.RecoveryTrigger,
                recoverySettings.Trigger == RecoveryTrigger.Manual ? "manual" : "automatic");
            // Prompt is chosen by interaction mode: oneshot → oneshot.md,
            // interactive → inline.md.
            var entrypoint  = interactive ? "inline" : "oneshot";
            // Quiet mode in one-shot: suppress streaming prose and agent-internal
            // event spam; the user sees only the spinner + the final AI help.
            var quiet = !interactive;

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

            var approval = new ConsoleApprovalPrompt(spinner, renderer, keyReader, consoleRenderer);

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
            var (startup, agent) = await RunWithStartupKeysAsync(keyReader, RequestCancellation, async () =>
            {
                var resolved = await AgentStartupBoundary.ResolveAsync(
                    agentOpts, auth, profileService, handedOverBoundary: null,
                    (tenant, token, ct) => AgentStartupBoundary.LookupDataBoundary(
                        AzclipsServiceProvider.GetRequiredService<DataBoundaryResolver>(), tenant, token, ct),
                    session, cts.Token, request?.ModelProfileName);

                // Telemetry retains the launch upload region; this lookup only selects the model endpoint.
                trace?.Note("data_boundary", new { boundary = resolved.DataBoundary });

                var azureAuth = resolved.Auth;
                // Preserves today's behavior exactly: a signed-out user still gets
                // BuildAuthErrorMessage()'s actionable "run az login", with its original stack, and
                // recovery still stops here — only now the outcome was recorded first.
                resolved.Failure?.Throw();

                var created = await ChatAgent.CreateAsync(
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
                    dataBoundary: resolved.DataBoundary);
                return (resolved, created);
            });
            agent.RecordModel(session);
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

            try
            {
                var end = await RunChatLoopAsync(agent, approval, keyReader, spinner, firstPrompt, interactive, cts.Token);
                ThrowForSessionEnd(end, interactive, agent.LastError,
                    agent.SubmitRendered || agent.HasPendingPlan, cts.Token);
            }
            finally
            {
                agent.RecordModel(session);
            }
        }

        void RequestCancellation() => _ = CancelAsync();

        async Task CancelAsync()
        {
            try
            {
                if (Volatile.Read(ref activeLifetime) is { } lifetime)
                    await lifetime.CancelAsync(cts).ConfigureAwait(false);
                else
                    await cts.CancelAsync().ConfigureAwait(false);
            }
            catch
            {
                Console.Error.WriteLine("Agent telemetry: cancellation_callback_failed");
            }
        }
    }

    /// <summary>
    /// True when an interactive session reads keys: stdin is a terminal and
    /// <see cref="Console.In"/> is still <paramref name="originalInput"/>.
    /// </summary>
    internal static bool StartsKeyReader(bool interactive, bool inputRedirected, TextReader currentInput, TextReader originalInput) =>
        interactive && ConsoleKeyReader.UsesKeyInput(inputRedirected, currentInput, originalInput);

    /// <summary>
    /// Throws when an interactive session has redirected input or output.
    /// </summary>
    internal static void RequireTerminal(bool interactive, bool inputRedirected, bool outputRedirected)
    {
        if (interactive && (inputRedirected || outputRedirected))
            throw new InvalidOperationException("Interactive recovery requires a terminal. Use --style oneshot when input or output is redirected.");
    }

    /// <summary>
    /// Runs the startup steps while keys go to the startup view: Esc or Ctrl+C calls
    /// <paramref name="cancel"/>, other keys are discarded. Keys after the interrupt stay queued.
    /// </summary>
    internal static async Task<T> RunWithStartupKeysAsync<T>(
        ConsoleKeyReader? keys, Action cancel, Func<Task<T>> startup)
    {
        if (keys is null)
            return await startup().ConfigureAwait(false);
        using var stop = new CancellationTokenSource();
        var watch = WatchStartupKeysAsync(keys, cancel, stop.Token);
        try
        {
            return await startup().ConfigureAwait(false);
        }
        finally
        {
            stop.Cancel();
            await watch.ConfigureAwait(false);
        }
    }

    private static async Task WatchStartupKeysAsync(ConsoleKeyReader keys, Action cancel, CancellationToken ct)
    {
        try
        {
            while (await keys.WaitToReadAsync(ct).ConfigureAwait(false))
            {
                if (AbortKeyWatcher.TakeTurnKeys(keys))
                {
                    cancel();
                    return;
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    internal static Task<int> RunSessionAsync(
        Func<ITelemetryLayerScope> getScope,
        Func<Activity?, AgentRuntimeLifetime, Task> runAgent,
        Action disposeServices,
        TimeProvider? timeProvider = null,
        Action? restoreTerminal = null) =>
        RunSessionCoreAsync(() => Task.FromResult(getScope()), runAgent, disposeServices, timeProvider, restoreTerminal);

    private static async Task<int> RunSessionCoreAsync(
        Func<Task<ITelemetryLayerScope>> getScope,
        Func<Activity?, AgentRuntimeLifetime, Task> runAgent,
        Action disposeServices,
        TimeProvider? timeProvider = null,
        Action? restoreTerminal = null)
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
                outcome = RecoveryOutcome.Ok;
                return 0;
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
                    // Completion runs under the lifetime's drain budget; scope disposal
                    // gets what is left of it. The terminal is restored afterwards even
                    // when disposal does not finish.
                    await lifetime.CompleteAsync(session, tags, outcome.Status).ConfigureAwait(false);
                    var budget = new TelemetryLayerBudget(
                        timeProvider ?? TimeProvider.System, lifetime.RemainingDrainTime);
                    if (!await budget.ObserveAsync(scope!.DisposeAsync().AsTask()).ConfigureAwait(false))
                        Console.Error.WriteLine("Agent telemetry: cleanup_deadline");
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
            try { restoreTerminal?.Invoke(); }
            catch { Console.Error.WriteLine("Agent: terminal_restore_failed"); }
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

    /// <summary>How the chat loop ended.</summary>
    internal enum ChatLoopEnd
    {
        Finished,
        CtrlCQuit,
    }

    /// <summary>The parts of <see cref="ChatAgent"/> the chat loop drives.</summary>
    internal interface IChatTurns
    {
        Task SendAsync(string prompt, CancellationToken ct);
        Task AbortAsync(CancellationToken ct);
        bool SessionClosed { get; }
        SessionErrored? LastError { get; }
        bool LastSendCalledClosingTool { get; }
        Activity? StartActivity(string name);
    }

    private sealed class ChatAgentTurns(ChatAgent agent) : IChatTurns
    {
        public Task SendAsync(string prompt, CancellationToken ct) => agent.SendAsync(prompt, ct);
        public Task AbortAsync(CancellationToken ct) => agent.AbortAsync(ct);
        public bool SessionClosed => agent.SessionClosed;
        public SessionErrored? LastError => agent.LastError;
        public bool LastSendCalledClosingTool => agent.LastSendCalledClosingTool;
        public Activity? StartActivity(string name) => agent.Telemetry.StartActivity(name);
    }

    /// <summary>Throws the cancellation that exits 130 when Ctrl+C quit the loop.</summary>
    internal static void ThrowIfQuitByCtrlC(ChatLoopEnd end)
    {
        if (end == ChatLoopEnd.CtrlCQuit)
            throw new OperationCanceledException();
    }

    /// <summary>
    /// Decides how the session ends. Ctrl+C quit and cancellation end with 130. An
    /// interactive session that the user quit otherwise succeeds, whatever earlier
    /// turns did. A oneshot session reports its last SDK error, or fails without an outcome.
    /// </summary>
    internal static void ThrowForSessionEnd(
        ChatLoopEnd end, bool interactive, SessionErrored? lastError, bool hasOutcome, CancellationToken ct)
    {
        ThrowIfQuitByCtrlC(end);
        if (interactive)
        {
            ct.ThrowIfCancellationRequested();
            return;
        }
        ThrowIfSessionEndedInError(lastError, ct);
        if (!hasOutcome)
            throw new InvalidOperationException("AI diagnosis ended without a recovery plan or final response.");
    }

    private static async Task<ChatLoopEnd> RunChatLoopAsync(
        ChatAgent agent,
        ConsoleApprovalPrompt approval,
        ConsoleKeyReader? keys,
        SpinnerService spinner,
        string firstPrompt,
        bool interactive,
        CancellationToken ct)
    {
        var end = await RunChatLoopAsync(new ChatAgentTurns(agent), approval, keys, spinner,
            firstPrompt, interactive, ct).ConfigureAwait(false);

        // Fallback render: if a oneshot plan is still pending (nothing rendered it),
        // show it here so one-shot never exits on a blank screen.
        if (!interactive && !agent.SubmitRendered && agent.HasPendingPlan)
            approval.ShowPlan(new ConsoleApprovalPrompt.PlanContent(
                agent.PendingIssues, agent.PendingPlanSteps, agent.PendingNeeds, agent.PendingDefaults));
        return end;
    }

    internal static async Task<ChatLoopEnd> RunChatLoopAsync(
        IChatTurns agent,
        ConsoleApprovalPrompt approval,
        ConsoleKeyReader? keys,
        SpinnerService? spinner,
        string firstPrompt,
        bool interactive,
        CancellationToken ct)
    {
        // Session-level interaction brake: counts user<->agent messages and never
        // resets. The first prompt is message 1; each new "> " line adds one.
        // Internal self-correction re-sends inside SendWithToolEnforcementAsync are
        // not counted (they aren't new user input).
        int userMessages = 1;
        // The very first turn is the error-recovery opener — show a spinner verb
        // that says so. Later turns (and any redirect) fall back to "thinking".
        var first = await SendWithToolEnforcementAsync(agent, approval, keys, spinner, firstPrompt, interactive, ct,
            initialVerb: "Analyzing error root cause using AI");
        if (first.Quit) return ChatLoopEnd.CtrlCQuit;
        var nextPrompt = first.Redirect;

        // oneshot closes on its first valid plan (rendered by the plan hook) or on
        // submit_response; drain any redirect until it does (or the agent stops
        // without one, e.g. an out-of-scope rejection), but never open a "> " prompt.
        if (!interactive)
        {
            while (!agent.SessionClosed && nextPrompt is not null && !ct.IsCancellationRequested)
            {
                var redirected = await SendWithToolEnforcementAsync(agent, approval, keys, spinner, nextPrompt, interactive, ct);
                if (redirected.Quit) return ChatLoopEnd.CtrlCQuit;
                nextPrompt = redirected.Redirect;
            }
            return ChatLoopEnd.Finished;
        }

        var draft = new LineEditor();
        // False while the same prompt is redrawn in place (Esc, Ctrl+C clearing the draft).
        var freshPrompt = true;
        while (!ct.IsCancellationRequested)
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
                if (freshPrompt)
                    Console.WriteLine();
                freshPrompt = true;
                string? read;
                if (keys is null)
                {
                    Console.Write("> ");
                    read = Console.ReadLine();
                }
                else
                {
                    var result = await keys.ReadLineAsync(draft, "> ", Console.Out, ct).ConfigureAwait(false);
                    if (result.Kind == LineReadKind.CtrlC)
                    {
                        var action = ConsoleKeyReader.DecideCtrlC(
                            approvalOpen: false, draftNonEmpty: !draft.IsEmpty, turnRunning: false);
                        if (action == CtrlCAction.Quit)
                        {
                            Console.WriteLine();
                            return ChatLoopEnd.CtrlCQuit;
                        }
                        draft.Clear();
                        freshPrompt = false;
                        continue;
                    }
                    if (result.Kind == LineReadKind.Escape)
                    {
                        freshPrompt = false;
                        continue;
                    }
                    read = result.Kind == LineReadKind.Submitted ? result.Text : null;
                    draft.Clear();
                }
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

            var turn = await SendWithToolEnforcementAsync(agent, approval, keys, spinner, line, interactive, ct);
            if (turn.Quit) return ChatLoopEnd.CtrlCQuit;
            nextPrompt = turn.Redirect;
        }
        return ChatLoopEnd.Finished;
    }

    /// <summary>Reports a final-turn SDK failure or caller cancellation after the chat loop.</summary>
    /// <remarks>
    /// Call once after the loop; it may return after catching a failed send.
    /// Pass <see cref="ChatAgent.LastError"/>, not <see cref="ChatAgent.StartupError"/>.
    /// Earlier failures do not override a successful final turn.
    /// A terminal SDK error takes precedence over caller cancellation.
    /// </remarks>
    internal static void ThrowIfSessionEndedInError(SessionErrored? terminalError, CancellationToken ct)
    {
        if (terminalError is { } err)
        {
            throw new AgentSessionFailedException(err.ErrorType, err.Message);
        }

        ct.ThrowIfCancellationRequested();
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
    private static async Task<TurnResult> SendWithToolEnforcementAsync(
        IChatTurns agent,
        ConsoleApprovalPrompt approval,
        ConsoleKeyReader? keys,
        SpinnerService? spinner,
        string prompt,
        bool interactive,
        CancellationToken ct,
        string initialVerb = "thinking")
    {
        using var turn = agent.StartActivity("Agent.Turn");
        var result = await SendAndPumpAsync(agent, approval, keys, spinner, prompt, interactive, ct, initialVerb);

        for (int attempt = 0; attempt < MaxToolEnforcementRetries; attempt++)
        {
            // A redirect means the user steered the turn — respect it, don't enforce.
            if (result.Redirect is not null) break;
            // A turn the user interrupted returns to the prompt.
            if (result.Interrupted || result.Quit) break;
            // One-shot already showed its final outcome (a valid plan ends the session).
            if (agent.SessionClosed) break;
            // Compliant turn: it closed with propose_plan or submit_response.
            // (get_command/bash alone do NOT count — a query-then-prose turn is a violation.)
            if (agent.LastSendCalledClosingTool) break;
            // Errored turn: don't pile a correction on top of a failure.
            if (result.Errored || agent.LastError is not null) break;

            using var reprompt = agent.StartActivity("Agent.Reprompt");
            reprompt?.SetTag(TelemetryTags.RecoveryReprompts, attempt + 1);

            WriteColor(ConsoleColor.DarkGray, "  ↯ agent replied without acting — asking it to use a tool");
            const string correction =
                "You replied with prose instead of calling a tool. That is not allowed. " +
                "Call propose_plan to suggest a fix (the user must approve before any command runs), " +
                "or submit_response to end the session. Do it now.";
            result = await SendAndPumpAsync(agent, approval, keys, spinner, correction, interactive, ct);
        }

        return result;
    }

    private readonly record struct TurnResult(string? Redirect, bool Interrupted, bool Quit = false, bool Errored = false);

    // Runs one turn. While it runs, keys go to the thinking view (Esc and Ctrl+C
    // interrupt the turn) and approval requests open the approval view. Keys queued
    // before an approval request are handled first. Redirect is the free text the
    // user typed at an approval; the caller replays it as the next turn.
    private static async Task<TurnResult> SendAndPumpAsync(
        IChatTurns agent,
        ConsoleApprovalPrompt approval,
        ConsoleKeyReader? keys,
        SpinnerService? spinner,
        string prompt,
        bool interactive,
        CancellationToken ct,
        string initialVerb = "thinking")
    {
        spinner?.Start(initialVerb);
        var sendTask = agent.SendAsync(prompt, ct);

        string? redirect = null;
        var interrupted = false;
        var keysOpen = keys is not null;
        using var waitCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        try
        {
            Task<bool>? keyWait = null;
            Task<bool>? requestWait = null;
            while (!sendTask.IsCompleted)
            {
                if (keysOpen)
                    keyWait ??= keys!.WaitToReadAsync(waitCts.Token).AsTask();
                requestWait ??= approval.WaitForRequestAsync(waitCts.Token);
                var waits = keyWait is null
                    ? new Task[] { sendTask, requestWait }
                    : new Task[] { sendTask, keyWait, requestWait };
                await Task.WhenAny(waits).ConfigureAwait(false);
                if (sendTask.IsCompleted) break;
                if (ct.IsCancellationRequested) break;

                if (keyWait is { IsCompleted: true })
                {
                    var more = keyWait.IsCompletedSuccessfully && keyWait.Result;
                    keyWait = null;
                    if (!more)
                        keysOpen = false;
                    else if (AbortKeyWatcher.TakeTurnKeys(keys!))
                    {
                        interrupted = true;
                        break;
                    }
                    continue;
                }

                if (requestWait.IsCompleted)
                {
                    requestWait = null;
                    var outcome = await PumpUntilSendEndsAsync(approval, sendTask, ct).ConfigureAwait(false);
                    if (outcome.InterruptTurn)
                    {
                        interrupted = true;
                        break;
                    }
                    if (outcome.FeedbackPrompt is { } feedback)
                    {
                        // Reject + redirect: abort the in-flight turn so the SDK
                        // stops calling tools, and hand the feedback back to the
                        // chat loop as the next prompt.
                        redirect = feedback;
                        break;
                    }
                }
            }
        }
        finally
        {
            waitCts.Cancel();
            spinner?.Stop();
        }

        var quit = false;
        if (interrupted || redirect is not null)
        {
            quit = await AbortWhileDispatchingKeysAsync(agent, keys, ct).ConfigureAwait(false);
            WriteColor(ConsoleColor.DarkGray, redirect is not null
                ? $"  ↯ told to do differently: {Truncate(redirect, 60)}"
                : "  ↯ aborted by user");
            if (quit)
            {
                // Quitting does not wait for a send that has not ended.
                _ = sendTask.ContinueWith(static t => _ = t.Exception, CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
                approval.DenyPending();
                return new TurnResult(redirect, interrupted, Quit: true);
            }
        }

        var errored = false;
        try { await sendTask.ConfigureAwait(false); }
        catch (OperationCanceledException) when (agent.LastError is { } err)
        {
            // Allow another interactive turn; a oneshot session reads LastError at the end.
            errored = true;
            WriteColor(ConsoleColor.Red, $"  ✗ session.error [{err.ErrorType}] {err.Message}");
        }
        catch (OperationCanceledException) when ((redirect is not null || interrupted) && !ct.IsCancellationRequested)
        {
            // Abort requested by the user — already announced; swallow.
        }
        catch (Exception ex) when (ex is not OperationCanceledException && interactive)
        {
            // In an interactive session, RPC and IO failures end only this turn; the session returns to the prompt.
            errored = true;
            WriteColor(ConsoleColor.Red, $"  ✗ session.error {ex.Message}");
        }
        finally
        {
            // Requests left by the finished turn are denied without prompting.
            approval.DenyPending();
        }

        return new TurnResult(redirect, interrupted, Errored: errored);
    }

    // Pumps one approval. When the turn ends while the approval is open, the approval
    // is closed without an answer.
    private static async Task<ConsoleApprovalPrompt.ApprovalOutcome> PumpUntilSendEndsAsync(
        ConsoleApprovalPrompt approval, Task sendTask, CancellationToken ct)
    {
        using var pumpCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var pump = approval.PumpOneAsync(pumpCts.Token, sendTask);
        if (await Task.WhenAny(pump, sendTask).ConfigureAwait(false) != pump)
            pumpCts.Cancel();
        return await pump.ConfigureAwait(false);
    }

    // Aborts the turn while keys still go to the thinking view. Ctrl+C there quits,
    // since the turn is already interrupted and no draft exists. Returns true on quit.
    private static async Task<bool> AbortWhileDispatchingKeysAsync(
        IChatTurns agent, ConsoleKeyReader? keys, CancellationToken ct)
    {
        var abort = agent.AbortAsync(ct);
        if (keys is null)
        {
            await abort.ConfigureAwait(false);
            return false;
        }

        using var waitCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        try
        {
            while (!abort.IsCompleted)
            {
                var keyWait = keys.WaitToReadAsync(waitCts.Token).AsTask();
                await Task.WhenAny(abort, keyWait).ConfigureAwait(false);
                if (abort.IsCompleted || !keyWait.IsCompletedSuccessfully || !keyWait.Result)
                    break;
                while (keys.TryRead(out var key))
                {
                    if (ConsoleKeyReader.IsCtrlC(key) &&
                        ConsoleKeyReader.DecideCtrlC(approvalOpen: false, draftNonEmpty: false, turnRunning: false)
                            == CtrlCAction.Quit)
                        return true;
                }
            }
        }
        finally
        {
            waitCts.Cancel();
        }
        await abort.ConfigureAwait(false);
        return false;
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
