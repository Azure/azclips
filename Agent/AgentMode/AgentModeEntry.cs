using System.Diagnostics;
using Core.Abstractions;
using Core.Agent;
using Core.Agent.Hosting;
using Core.Agent.Providers;
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
    // Session-level interaction brake (MVP constants, not configurable yet):
    // counts user<->agent messages (each new user message +1, internal
    // self-correction re-sends excluded), does NOT reset, soft reminder at
    // SoftTurns, one hard stop at HardTurns.
    private const int EscapeValveSoftTurns = 12;
    private const int EscapeValveHardTurns = 20;

    public static async Task<int> RunAsync(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        var failedCommand = ArgValue(args, "--failed-command") ?? string.Empty;
        var errorText     = ArgValue(args, "--error")          ?? string.Empty;

        // Runs as its own process (azclips-agent.exe), spawned by the CLI on command
        // failure, so it initializes as ClientType.CLI. AgentTelemetryProvider replaces
        // the default one because only it attaches a listener; without it every span
        // in this process is null.
        AzclipsServiceProvider.OverrideRegistration(services =>
        {
            services.AddSingleton<ITelemetrySender, FileTelemetrySender>();
            services.AddSingleton<ITelemetryProvider, AgentTelemetryProvider>();
        });
        AzclipsServiceProvider.Initialize(ClientType.CLI);

        TraceAgentRenderer? trace = null;
        return await RunSessionAsync(
            () => AzclipsServiceProvider.GetRequiredService<ITelemetryProvider>(),
            RunAgentAsync,
            () =>
            {
                trace?.Dispose();
                AzclipsServiceProvider.Dispose();
            });

        async Task RunAgentAsync(Activity? session)
        {
            var auth        = AzclipsServiceProvider.GetRequiredService<IAuthService>();
            var profileService = AzclipsServiceProvider.GetRequiredService<IProfileService>();
            var config      = AzclipsServiceProvider.GetRequiredService<IConfigurationService>();
            var agentOpts   = config.GetConfiguration<AgentOptions>();
            RecoveryTelemetry.RecordConfiguration(session, agentOpts);
            // Prompt is chosen by interaction mode: oneshot → oneshot.md,
            // interactive → inline.md.
            var entrypoint  = agentOpts.IsInteractive ? "inline" : "oneshot";
            // Quiet mode in one-shot: suppress streaming prose and agent-internal
            // event spam; the user sees only the spinner + the final AI help.
            var quiet = !agentOpts.IsInteractive;

            var flavor   = Environment.GetEnvironmentVariable("AZURECLI_FLAVOR") ?? "azure cli";
            var az     = FindAzExe() ?? "az";
            var consoleRenderer = new ConsoleAgentRenderer(quiet, agentOpts.Interactive.Verbose);
            // When agent.trace.enabled, wrap the console renderer to record a JSONL
            // trace under agent.trace.path. Prose suppression after submit_response
            // lives on ConsoleAgentRenderer itself (always-on), independent of trace.
            IAgentRenderer renderer = consoleRenderer;
            if (agentOpts.Trace.Enabled && !string.IsNullOrWhiteSpace(agentOpts.Trace.Path))
            {
                // Expand a leading ~ (the documented default is ~/.azclips/trace) — .NET
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
            // await using: the spinner is Started before CreateAsync (below), so it
            // must be Stopped even when CreateAsync throws or the user Ctrl+Cs during
            // cold start. Disposal runs on the way out of this scope — before the
            // catch blocks — so the spinner line is cleared and the cursor restored
            // before the error is printed.
            await using var spinner = new SpinnerService(renderer);

            // Watcher is shared across all turns and paused (not destroyed) while
            // the approval pump reads y/n — that's how single-reader stdin is
            // preserved. The abort callback closes over agentRef so we can wire
            // the watcher before the agent exists.
            ChatAgent? agentRef = null;
            var watcher  = new AbortKeyWatcher(
                onAbort: ct => agentRef!.AbortAsync(ct),
                onQuit:  () => cts.Cancel());
            var approval = new ConsoleApprovalPrompt(spinner, renderer, abortWatcher: watcher);

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

            // Reuse one Azure token for the builtin provider and this diagnostic ARM probe.
            // The probe does not select the model endpoint.
            // Do not add a second concurrent MSAL acquisition over the shared token cache.
            var startup = await AgentStartupBoundary.ResolveAsync(
                agentOpts, auth, profileService, ResolverFactory, session, cts.Token);

            // The only place this tag is written. Unconditional, because the seam never throws:
            // there is no path from here to ChatAgent.CreateAsync that reaches it without a value.
            session?.SetTag(TelemetryTags.RecoveryDataBoundary, startup.Boundary.Value);

            trace?.Note("data_boundary", new { boundary = startup.Boundary.Value });

            var azureAuth = startup.Auth;
            // Preserves today's behavior exactly: a signed-out user still gets
            // BuildAuthErrorMessage()'s actionable "run az login", with its original stack, and
            // recovery still stops here — only now the outcome was recorded first.
            startup.Failure?.Throw();

            var agent = await ChatAgent.CreateAsync(
                flavor: flavor,
                auth: auth,
                profileService: profileService,
                azExePath: az,
                approvalSink: approval,
                renderer: renderer,
                options: agentOpts,
                entrypoint: entrypoint,
                spinner: spinner,
                failedCommand: failedCommand,
                ct: cts.Token,
                preAcquiredAuth: azureAuth);
            agent.RecordModel(session);
            await using var agentScope = agent;
            agentRef = agent;
            // Always-on prose suppression: after submit_response renders its
            // outcome, ConsoleAgentRenderer drops any trailing prose the model
            // streams past the terminator — independent of whether tracing is on.
            agent.SetOnFinalRendered(consoleRenderer.MarkFinalRendered);
            trace?.Note("session_start", new { entrypoint, flavor, sessionStyle = agentOpts.SessionStyle });

            var firstPrompt = string.IsNullOrWhiteSpace(errorText)
                ? $"I tried `{failedCommand}` but it failed. Help me fix it."
                : $"I tried `{failedCommand}` but it failed:\n{errorText}\nHelp me fix it.";

            var interactive = agentOpts.IsInteractive;

            try
            {
                await RunChatLoopAsync(agent, approval, watcher, spinner, firstPrompt, interactive, cts.Token);

                ThrowIfSessionEndedInError(agent.LastError, cts.Token);
            }
            finally
            {
                agent.RecordModel(session);
            }
        }
    }

    internal static async Task<int> RunSessionAsync(
        Func<ITelemetryProvider> getTelemetry,
        Func<Activity?, Task> runAgent,
        Action disposeServices)
    {
        Activity? session = null;
        // Leave unclassified exits observable instead of reporting success.
        var outcome = RecoveryOutcome.Unknown;
        try
        {
            try
            {
                // Resolve here so initialization failures use the normal error path.
                // Resolution attaches the listener before Agent.Session starts.
                var telemetry = getTelemetry();
                session = telemetry.ActivitySource.StartActivity(
                    "Agent.Session", ActivityKind.Server, RecoveryTraceContext.FromEnvironment());
                session?.SetTag(TelemetryTags.ProcessName, "azclips-agent");

                await runAgent(session);
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
            finally
            {
                // Write terminal tags before stopping the session and disposing its provider.
                session?.SetTag(TelemetryTags.RecoveryOutcome, outcome.Value);
                if (outcome.FailureReason is { } reason)
                    session?.SetTag(TelemetryTags.RecoveryFailureReason, reason);
                if (outcome.Status is { } status)
                    session?.SetStatus(status);
            }
        }
        finally
        {
            session?.Dispose();
            disposeServices();
        }
    }

    private static IDataBoundaryResolver ResolverFactory(IProfileService profileService) =>
        new Core.Agent.Providers.DataBoundaryResolver(profileService);

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

    private static async Task RunChatLoopAsync(
        ChatAgent agent,
        ConsoleApprovalPrompt approval,
        AbortKeyWatcher watcher,
        SpinnerService spinner,
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
        var nextPrompt = await SendWithToolEnforcementAsync(agent, approval, watcher, spinner, firstPrompt, ct,
            initialVerb: "Analyzing error root cause using AI");

        // submit_response is the sole terminator. oneshot proposes a fix then closes
        // with submit_response; drain any redirect until it does (or the agent stops
        // without one, e.g. an out-of-scope rejection), but never open a "> " prompt.
        if (!interactive)
        {
            while (!agent.SessionClosed && nextPrompt is not null && !ct.IsCancellationRequested)
                nextPrompt = await SendWithToolEnforcementAsync(agent, approval, watcher, spinner, nextPrompt, ct);

            // Fallback render: if the agent proposed a plan but never submitted it
            // (stopped after propose_plan), submit_response never rendered it and it
            // would be lost. Show it here so one-shot never exits on a blank screen.
            if (!agent.SubmitRendered && agent.HasPendingPlan)
                approval.ShowPlan(new ConsoleApprovalPrompt.PlanContent(
                    agent.PendingIssues, agent.PendingPlanSteps, agent.PendingNeeds, agent.PendingDefaults));

            return;
        }

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
                Console.WriteLine();
                Console.Write("> ");
                var read = Console.ReadLine();
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

            nextPrompt = await SendWithToolEnforcementAsync(agent, approval, watcher, spinner, line, ct);
        }
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
    private static async Task<string?> SendWithToolEnforcementAsync(
        ChatAgent agent,
        ConsoleApprovalPrompt approval,
        AbortKeyWatcher watcher,
        SpinnerService spinner,
        string prompt,
        CancellationToken ct,
        string initialVerb = "thinking")
    {
        using var turn = AgentTracing.Source.StartActivity("Agent.Turn");
        var redirect = await SendAndPumpAsync(agent, approval, watcher, spinner, prompt, ct, initialVerb);

        for (int attempt = 0; attempt < MaxToolEnforcementRetries; attempt++)
        {
            // A redirect means the user steered the turn — respect it, don't enforce.
            if (redirect is not null) break;
            // Compliant turn: it closed with propose_plan or submit_response.
            // (get_command/bash alone do NOT count — a query-then-prose turn is a violation.)
            if (agent.LastSendCalledClosingTool) break;
            // Errored turn: don't pile a correction on top of a failure.
            if (agent.LastError is not null) break;

            using var reprompt = AgentTracing.Source.StartActivity("Agent.Reprompt");
            reprompt?.SetTag(TelemetryTags.RecoveryReprompts, attempt + 1);

            WriteColor(ConsoleColor.DarkGray, "  ↯ agent replied without acting — asking it to use a tool");
            const string correction =
                "You replied with prose instead of calling a tool. That is not allowed. " +
                "Call propose_plan to suggest a fix (the user must approve before any command runs), " +
                "or submit_response to end the session. Do it now.";
            redirect = await SendAndPumpAsync(agent, approval, watcher, spinner, correction, ct);
        }

        return redirect;
    }

    // Returns a feedback prompt when the user typed free text at a y/n
    // approval; null otherwise. The caller replays that text as the next turn.
    private static async Task<string?> SendAndPumpAsync(
        ChatAgent agent,
        ConsoleApprovalPrompt approval,
        AbortKeyWatcher watcher,
        SpinnerService spinner,
        string prompt,
        CancellationToken ct,
        string initialVerb = "thinking")
    {
        spinner.Start(initialVerb);
        var sendTask = agent.SendAsync(prompt, ct);

        var watcherCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var watcherTask = watcher.RunAsync(watcherCts.Token);

        string? redirect = null;
        try
        {
            while (!sendTask.IsCompleted)
            {
                using var pumpCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                var pumpTask = approval.PumpOneAsync(pumpCts.Token);
                var completed = await Task.WhenAny(sendTask, pumpTask, watcherTask).ConfigureAwait(false);

                if (completed == sendTask)
                {
                    pumpCts.Cancel();
                    try { await pumpTask.ConfigureAwait(false); } catch { }
                    break;
                }

                if (completed == watcherTask)
                {
                    pumpCts.Cancel();
                    try { await pumpTask.ConfigureAwait(false); } catch { }
                    WriteColor(ConsoleColor.DarkGray, "  ↯ aborted by user");
                    break;
                }

                var outcome = await pumpTask.ConfigureAwait(false);
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
            watcherCts.Cancel();
            try { await watcherTask.ConfigureAwait(false); } catch { }
            watcherCts.Dispose();
            spinner.Stop();
        }

        try { await sendTask.ConfigureAwait(false); }
        catch (OperationCanceledException) when (agent.LastError is { } err)
        {
            // Allow another interactive turn; the final check still reads LastError.
            WriteColor(ConsoleColor.Red, $"  ✗ session.error [{err.ErrorType}] {err.Message}");
        }
        catch (OperationCanceledException) when (redirect is not null)
        {
            // Abort caused by user redirect — already announced; swallow.
        }

        return redirect;
    }

    // .NET does not expand a leading "~"; do it ourselves so the documented default
    // (~/.azclips/trace) resolves under the user profile instead of a literal "~" dir.
    private static string ExpandHome(string path)
    {
        if (path == "~")
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (path.StartsWith("~/", StringComparison.Ordinal) || path.StartsWith("~\\", StringComparison.Ordinal))
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[2..]);
        return path;
    }

    private static string? ArgValue(string[] args, string flag)
    {
        var idx = Array.IndexOf(args, flag);
        if (idx < 0 || idx + 1 >= args.Length) return null;
        return args[idx + 1];
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
