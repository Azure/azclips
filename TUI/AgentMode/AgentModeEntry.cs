using Core.Abstractions;
using Core.Agent;
using Core.Agent.Hosting;
using Core.DependencyInjection;
using Core.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Tui.AgentMode;

/// <summary>
/// TUI-process entry point for agent mode — spawned by the classic CLI when an
/// invocation fails. Owns the chat loop, banner, and stdin so the approval pump
/// and prompt share a single reader.
/// </summary>
public static class AgentModeEntry
{
    // Interactive escape valve thresholds (MVP constants, not configurable yet):
    // a soft reminder at SoftTurns, a hard abort at HardTurns.
    private const int EscapeValveSoftTurns = 12;
    private const int EscapeValveHardTurns = 20;

    public static async Task<int> RunAsync(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        var failedCommand = ArgValue(args, "--failed-command") ?? string.Empty;
        var errorText     = ArgValue(args, "--error")          ?? string.Empty;

        CirrusServiceProvider.Initialize(ClientType.TUI);

        try
        {
            PrintBanner(failedCommand, errorText);

            var auth        = CirrusServiceProvider.GetRequiredService<IAuthService>();
            var authContext = CirrusServiceProvider.GetRequiredService<IAuthContextService>();
            var config      = CirrusServiceProvider.GetRequiredService<IConfigurationService>();
            var agentOpts   = config.GetConfiguration<AgentOptions>();
            // Prompt is chosen by interaction mode: oneShot → oneshot.md,
            // interactive → inline.md. (tui.md exists for a future full-screen
            // entry point that nothing spawns yet.)
            var entrypoint  = string.Equals(agentOpts.SessionStyle, "interactive", StringComparison.OrdinalIgnoreCase)
                ? "inline" : "oneshot";
            var flavor   = Environment.GetEnvironmentVariable("AZURECLI_FLAVOR") ?? "azure cli";
            var azv2     = FindAzv2Exe() ?? "azv2";
            var renderer = new ConsoleAgentRenderer();
            var spinner  = new SpinnerService(renderer);

            // Watcher is shared across all turns and paused (not destroyed) while
            // the approval pump reads y/n — that's how single-reader stdin is
            // preserved. The abort callback closes over agentRef so we can wire
            // the watcher before the agent exists.
            ChatAgent? agentRef = null;
            var watcher  = new AbortKeyWatcher(
                onAbort: ct => agentRef!.AbortAsync(ct),
                onQuit:  () => cts.Cancel());
            var approval = new ConsoleApprovalPrompt(spinner, renderer, abortWatcher: watcher);

            // Funnel cirrus-internal logs through the renderer so they don't
            // race the spinner. Verbose/Debug/Info dropped; Warn/Error become
            // HostLogEmitted events the renderer draws under its lock.
            using var _logScope = ScopedLoggingService.UseScope(new RendererLoggingService(renderer));

            await using var agent = await ChatAgent.CreateAsync(
                flavor: flavor,
                auth: auth,
                authContext: authContext,
                azv2ExePath: azv2,
                approvalSink: approval,
                renderer: renderer,
                options: agentOpts,
                entrypoint: entrypoint,
                spinner: spinner,
                ct: cts.Token);
            agentRef = agent;

            var firstPrompt = string.IsNullOrWhiteSpace(errorText)
                ? $"I tried `{failedCommand}` but it failed. Help me fix it."
                : $"I tried `{failedCommand}` but it failed:\n{errorText}\nHelp me fix it.";

            var interactive = string.Equals(agentOpts.SessionStyle, "interactive", StringComparison.OrdinalIgnoreCase);

            // Escape valve (interactive only): a soft reminder partway through and
            // a hard stop if the agent keeps looping. Numbers are MVP constants.
            if (interactive)
                agent.LlmTurnCompletedCount += turns =>
                {
                    if (turns == EscapeValveSoftTurns)
                        WriteColor(ConsoleColor.DarkGray, $"  agent has taken {turns} turns. Press Ctrl+C to interrupt.");
                    else if (turns >= EscapeValveHardTurns)
                    {
                        WriteColor(ConsoleColor.Yellow, $"  agent gave up after {turns} turns. Hit Ctrl+C anytime to interrupt sooner.");
                        _ = agent.AbortAsync(cts.Token);
                    }
                };

            await RunChatLoopAsync(agent, approval, watcher, spinner, firstPrompt, interactive, cts.Token);
            return 0;
        }
        catch (OperationCanceledException)
        {
            return 130;
        }
        catch (Exception ex)
        {
            WriteColor(ConsoleColor.Red, $"agent mode error: {ex.Message}");
            return 1;
        }
        finally
        {
            CirrusServiceProvider.Dispose();
        }
    }

    private static async Task RunChatLoopAsync(
        ChatAgent agent,
        ConsoleApprovalPrompt approval,
        AbortKeyWatcher watcher,
        SpinnerService spinner,
        string firstPrompt,
        bool interactive,
        CancellationToken ct)
    {
        var nextPrompt = await SendWithToolEnforcementAsync(agent, approval, watcher, spinner, firstPrompt, ct);

        // oneShot: the agent proposes a fix and exits. Once a plan has been shown
        // it IS the terminal output — stop immediately. Otherwise (no plan, e.g.
        // an out-of-scope rejection or a clarifying question) drain any redirect
        // but never open a fresh "> " prompt. A hint points at interactive mode.
        if (!interactive)
        {
            while (!agent.OneShotPlanShown && nextPrompt is not null && !ct.IsCancellationRequested)
                nextPrompt = await SendWithToolEnforcementAsync(agent, approval, watcher, spinner, nextPrompt, ct);

            Console.WriteLine();
            WriteColor(ConsoleColor.DarkGray,
                "  ↳ Run 'azv2 config set --key agent.sessionStyle --value interactive' for an interactive session.");
            return;
        }

        while (!ct.IsCancellationRequested)
        {
            string line;
            if (nextPrompt is not null)
            {
                // User typed free text at the y/n prompt — replay it as the
                // next chat turn without re-reading stdin.
                line = nextPrompt;
                nextPrompt = null;
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
            }

            nextPrompt = await SendWithToolEnforcementAsync(agent, approval, watcher, spinner, line, ct);
        }
    }

    // Max times we re-prompt a bare-prose turn before giving up and letting it
    // through (avoid an infinite correction loop if the model keeps disobeying).
    private const int MaxToolEnforcementRetries = 2;

    // Wraps SendAndPumpAsync with the plan-protocol guard: if a turn produced
    // only prose (no tool call, no submit_response), the model broke the "always
    // act through tools" contract — re-prompt it to call propose_plan/submit_response.
    // The SDK gives no way to forbid bare text up front, so this is the host-side
    // backstop. Returns the same redirect signal SendAndPumpAsync does.
    private static async Task<string?> SendWithToolEnforcementAsync(
        ChatAgent agent,
        ConsoleApprovalPrompt approval,
        AbortKeyWatcher watcher,
        SpinnerService spinner,
        string prompt,
        CancellationToken ct)
    {
        var redirect = await SendAndPumpAsync(agent, approval, watcher, spinner, prompt, ct);

        for (int attempt = 0; attempt < MaxToolEnforcementRetries; attempt++)
        {
            // A redirect means the user steered the turn — respect it, don't enforce.
            if (redirect is not null) break;
            // oneShot plan was shown: the hook rendered it and cancelled the turn,
            // so no closing tool executed (a denied propose_plan emits no
            // ToolStarted). That IS a complete turn — don't mistake it for prose.
            if (agent.OneShotPlanShown) break;
            // Compliant turn: it closed with propose_plan or submit_response.
            // (get_command/bash alone do NOT count — a query-then-prose turn is a violation.)
            if (agent.LastSendCalledClosingTool) break;
            // Errored turn: don't pile a correction on top of a failure.
            if (agent.LastError is not null) break;

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
        CancellationToken ct)
    {
        spinner.Start("thinking");
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
            WriteColor(ConsoleColor.Red, $"  ✗ session.error [{err.ErrorType}] {err.Message}");
        }
        catch (OperationCanceledException) when (redirect is not null)
        {
            // Abort caused by user redirect — already announced; swallow.
        }
        catch (OperationCanceledException) when (agent.OneShotPlanShown)
        {
            // oneShot plan was shown — the hook cancelled the turn on purpose so
            // the agent couldn't continue. Normal exit; swallow.
        }

        return redirect;
    }

    private static void PrintBanner(string failedCommand, string error)
    {
        var prev = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine();
        Console.WriteLine("🤖 agent mode");
        if (!string.IsNullOrWhiteSpace(failedCommand))
            Console.WriteLine($"   triggered by failed command: {failedCommand}");
        if (!string.IsNullOrWhiteSpace(error))
            Console.WriteLine($"   error: {Truncate(error, 200)}");
        Console.WriteLine("   type 'exit' or Ctrl+C to leave");
        Console.WriteLine();
        Console.ForegroundColor = prev;
    }

    private static string? ArgValue(string[] args, string flag)
    {
        var idx = Array.IndexOf(args, flag);
        if (idx < 0 || idx + 1 >= args.Length) return null;
        return args[idx + 1];
    }

    private static string? FindAzv2Exe()
    {
        var baseDir = AppContext.BaseDirectory;
        var name = OperatingSystem.IsWindows() ? "azv2.exe" : "azv2";
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
