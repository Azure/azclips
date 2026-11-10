using System.Text.RegularExpressions;
using System.Threading.Channels;
using GitHub.Copilot;

namespace Core.Agent.Hosting;

/// <summary>
/// SDK <c>OnPermissionRequest</c> callback runs on a background dispatch thread
/// owned by the daemon. Calling <see cref="Console.ReadLine"/> there races
/// against any other reader the host loop might run, and on Windows the global
/// stdin reader can return EOF instead of blocking when two threads contend.
///
/// This sink decouples the producer (SDK callback) from the consumer (host's
/// main thread): callbacks post a request + <see cref="TaskCompletionSource{TResult}"/>
/// onto an unbounded channel and await the answer. The host pumps the channel
/// on its own thread via <see cref="PumpOneAsync"/>, where it can safely write
/// the prompt and read the answer. Only the pump thread ever touches stdin,
/// so the stdin race goes away by construction.
///
/// Pattern:    background producer → channel → single-threaded consumer.
/// Avoid:      ❌ calling <see cref="Console.ReadLine"/> from the SDK callback
///                directly (the bug we're fixing);
///             ❌ blocking the SDK callback synchronously waiting on the host —
///                a single Channel + TaskCompletionSource lets the callback
///                await without occupying a daemon thread.
/// </summary>
public sealed class ConsoleApprovalPrompt
{
    private readonly SpinnerService? _spinner;
    private readonly IAgentRenderer? _renderer;
    private readonly AbortKeyWatcher? _abortWatcher;

    public ConsoleApprovalPrompt(
        SpinnerService? spinner = null,
        IAgentRenderer? renderer = null,
        AbortKeyWatcher? abortWatcher = null)
    {
        _spinner = spinner;
        _renderer = renderer;
        _abortWatcher = abortWatcher;
    }

    private readonly Channel<PendingApproval> _queue =
        Channel.CreateUnbounded<PendingApproval>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

    private record PendingApproval(
        string ToolName,
        string ArgsPreview,
        PlanContent? Plan,
        TaskCompletionSource<bool> Result,
        CancellationTokenRegistration Reg);

    // The structured content of a proposed plan, rendered as styled content blocks
    // (see the unified display system). Replaces the old free-form planMd string.
    public sealed record PlanContent(
        IReadOnlyList<string>? Issues,
        IReadOnlyList<PlanStep>? Steps,
        IReadOnlyList<UserInputNeed>? Placeholders,
        IReadOnlyList<ChosenDefault>? Defaults);

    // Minimal step view the host renders under the plan. Decoupled from the tool's
    // StepMeta so the hook owns the JSON parsing. Destructive drives the red marker.
    public sealed record PlanStep(int Id, string Label, string Cmd, bool Destructive = false);

    // A secret only the user can supply — Flag names the flag (e.g. --admin-password);
    // its value appears as a <placeholder> alongside the flag in the step command.
    public sealed record UserInputNeed(string Flag, string Description);

    // A flag the agent chose/derived for the user. Value is NOT stored — it's read
    // from the matching flag in the plan's step command at render time, so the
    // review line always matches what will actually run.
    public sealed record ChosenDefault(string Flag, string Note);

    // Outcome of one approval prompt. When Approved=false and FeedbackPrompt is
    // set, the user typed free-form text at the y/n prompt — the host treats it
    // as a rejection of the current tool plus the next chat-loop input.
    public sealed record ApprovalOutcome(bool Handled, bool Approved, string? FeedbackPrompt)
    {
        public static readonly ApprovalOutcome NotHandled = new(false, false, null);
        public static readonly ApprovalOutcome Yes = new(true, true, null);
        public static readonly ApprovalOutcome No = new(true, false, null);
        public static ApprovalOutcome Redirect(string feedback) => new(true, false, feedback);
    }

    /// <summary>
    /// Called from the SDK background callback. Returns a task that completes
    /// when the host pumps the next request and the user answers.
    /// </summary>
    public Task<bool> RequestAsync(PermissionRequest req, CancellationToken ct)
    {
        var (toolName, args) = Describe(req);

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reg = ct.Register(() => tcs.TrySetCanceled(ct));

        _queue.Writer.TryWrite(new PendingApproval(toolName, args, Plan: null, tcs, reg));
        return tcs.Task;
    }

    /// <summary>
    /// Approval path for OnPreToolUse rewrites. SDK semantics force the hook to
    /// own approval when it modifies args ("ask" discards modifiedArgs), so the
    /// hook routes through this method instead of OnPermissionRequest.
    /// </summary>
    public Task<bool> RequestRewriteAsync(string toolName, string rewrittenCommand, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reg = ct.Register(() => tcs.TrySetCanceled(ct));

        _queue.Writer.TryWrite(new PendingApproval(toolName, rewrittenCommand ?? string.Empty, Plan: null, tcs, reg));
        return tcs.Task;
    }

    /// <summary>
    /// Renders a plan without prompting for approval — used by one-shot mode,
    /// where the plan is the terminal output. Pauses the spinner and renders
    /// under the renderer lock (same protection PumpOneAsync uses) so the output
    /// doesn't race the spinner.
    /// </summary>
    public void ShowPlan(PlanContent plan)
    {
        _spinner?.Pause();
        _abortWatcher?.Pause();
        try
        {
            Action body = () =>
            {
                Console.WriteLine();
                RenderPlan(plan);
            };
            if (_renderer is not null) _renderer.RunExclusive(body);
            else                       body();
        }
        finally
        {
            _abortWatcher?.Resume();
            _spinner?.Resume();
        }
    }

    /// <summary>
    /// Renders the session's final outcome for submit_response — the single
    /// terminator under the unified-mind model. completed shows the plan content
    /// blocks + a disclaimer, rejected explains the out-of-scope reason, failed
    /// states the failure. Pauses the spinner and renders under the renderer lock.
    /// </summary>
    public void ShowFinalOutcome(
        string status,
        string? message,
        IReadOnlyList<string>? issues,
        IReadOnlyList<PlanStep>? planSteps,
        IReadOnlyList<UserInputNeed>? placeholders,
        IReadOnlyList<ChosenDefault>? defaults,
        bool isOneShot)
    {
        _spinner?.Pause();
        _abortWatcher?.Pause();
        try
        {
            Action body = () =>
            {
                Console.WriteLine();
                RenderFinalOutcome(status, message,
                    new PlanContent(issues, planSteps, placeholders, defaults), isOneShot);
            };
            if (_renderer is not null) _renderer.RunExclusive(body);
            else                       body();
        }
        finally
        {
            _abortWatcher?.Resume();
            _spinner?.Resume();
        }
    }

    private static void RenderFinalOutcome(string status, string? message, PlanContent plan, bool isOneShot)
    {
        if (string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase))
        {
            if (isOneShot)
            {
                // one-shot: the plan IS the deliverable (the user runs it), so
                // render it as the summary. RenderPlan ends with the disclaimer.
                RenderPlan(plan);
            }
            else
            {
                // interactive: the agent already executed the commands and their
                // output (plus the plan + its disclaimer, shown before approval)
                // is on screen. Just a terse completion marker.
                WriteColor(ConsoleColor.Green, "✓ Done");
            }

            // A completed message carries info NOT already in the plan (e.g. "the
            // storage account was renamed to foo3a7 because the name was taken").
            // The tool description tells the model to leave it empty when it would
            // just repeat the plan.
            if (!string.IsNullOrWhiteSpace(message))
                Console.WriteLine(message);
        }
        else if (string.Equals(status, "rejected", StringComparison.OrdinalIgnoreCase))
        {
            WriteColor(ConsoleColor.DarkYellow, "✗ Out of scope");
            Console.WriteLine($"  {ExplainOutOfScope(message)}");
        }
        else
        {
            WriteColor(ConsoleColor.Red, "✗ Couldn't finish");
            if (!string.IsNullOrWhiteSpace(message))
                Console.WriteLine($"  {message}");
        }
    }

    internal const string Disclaimer = "AI-generated content may be incorrect";

    // The "⚠ Values to review before running" block (plan.review): the values the
    // user should look at before running — both the ones the agent chose/derived
    // (shown as the real value, plain) and the secrets the user must fill in
    // (shown as <name> placeholders). Empty block is skipped.
    private static void RenderReviewValues(
        IReadOnlyList<PlanStep>? steps,
        IReadOnlyList<ChosenDefault>? defaults,
        IReadOnlyList<UserInputNeed>? placeholders)
    {
        var hasDefaults = defaults is { Count: > 0 };
        var hasPlaceholders = placeholders is { Count: > 0 };
        if (!hasDefaults && !hasPlaceholders) return;

        Console.WriteLine();
        WriteColor(ConsoleColor.DarkYellow, "⚠ Values to review before running:");
        if (hasDefaults)
            foreach (var d in defaults!)
            {
                // Show the flag exactly as it appears in the step command — with its
                // real value if it takes one, bare if it's a switch — so the review
                // line matches what runs. The value is never stored on ChosenDefault;
                // it's read from the command here.
                var token = FindFlagToken(steps, d.Flag);
                var note = string.IsNullOrWhiteSpace(d.Note) ? "" : $": {OneLine(d.Note)}";
                Console.WriteLine($"• {OneLine(token)}{note}");
            }
        if (hasPlaceholders)
            foreach (var p in placeholders!)
            {
                // Show the flag with its value as it appears in the step command (e.g.
                // "--admin-password <admin-password>"). If the step didn't include the flag,
                // FindFlagToken returns the bare flag; append a <flag-derived> placeholder so
                // the "you must fill this in" cue isn't lost.
                var token = FindFlagToken(steps, p.Flag);
                var display = token.Contains(' ') || token.Contains('<')
                    ? token
                    : $"{token} <{OneLine(token.TrimStart('-'))}>";
                Console.WriteLine($"• {OneLine(display)}: {OneLine(p.Description)}");
            }
    }

    // Locate a flag in the plan's step commands and return the flag with its value
    // if any: "--location eastus2", "--admin-password <pw>", or a bare "--active"
    // for a switch. The flag is normalized to a leading "--" for matching (a model
    // may list it without dashes), and "--flag=value" is rendered as "--flag value".
    // Falls back to the flag alone if not found. internal for unit testing.
    internal static string FindFlagToken(IReadOnlyList<PlanStep>? steps, string flag)
    {
        if (string.IsNullOrWhiteSpace(flag)) return flag;
        // Normalize to a leading "--" form; this normalized value is used both for
        // matching and for the fallback display when the flag isn't found.
        var needle = flag.StartsWith('-') ? flag : "--" + flag;
        if (steps is not null)
        {
            foreach (var s in steps)
            {
                if (string.IsNullOrEmpty(s.Cmd)) continue;
                // Match the flag as a WHOLE token — the trailing (?![\w-]) stops
                // "--resource" from matching inside "--resource-group". Then an
                // optional value that isn't itself a flag ((?!-) rejects both
                // "--x" and short "-x"). A quoted value ("..." or '...') is taken
                // whole so a value with spaces (e.g. --query "[?a].b c") isn't cut
                // at the first space; otherwise a bare non-space token. Group "val"
                // is absent for a switch.
                var m = Regex.Match(s.Cmd,
                    $@"(?<![^\s]){Regex.Escape(needle)}(?![\w-])(?:[=\s]+(?<val>(?!-)(?:""[^""]*""|'[^']*'|\S+)))?",
                    RegexOptions.IgnoreCase);
                if (m.Success)
                    return m.Groups["val"].Success ? $"{needle} {m.Groups["val"].Value}" : needle;
            }
        }
        return needle; // not found in any step — show the flag alone
    }

    // Collapse newlines/tabs/runs of whitespace to single spaces so a stray
    // multi-line name or description can't break the single-line layout.
    private static string OneLine(string? s) =>
        string.IsNullOrEmpty(s) ? string.Empty : WhitespaceRun.Replace(s, " ").Trim();

    private static readonly Regex WhitespaceRun = new(@"\s+", RegexOptions.Compiled);

    // Turns an OUT_OF_SCOPE_<code> marker into a human sentence. Unknown codes
    // keep the raw code so the user still sees why the request was declined.
    internal static string ExplainOutOfScope(string? message)
    {
        var code = message;
        if (!string.IsNullOrEmpty(code) &&
            code.StartsWith("OUT_OF_SCOPE_", StringComparison.OrdinalIgnoreCase))
            code = code["OUT_OF_SCOPE_".Length..];

        return code?.ToLowerInvariant() switch
        {
            "harm"     => "This request looks harmful or unsafe, so I won't act on it.",
            "other"    => "This isn't an Azure or terminal task I can help with.",
            null or "" => "This request is outside what I can help with.",
            _          => $"This request is outside what I can help with ({code}).",
        };
    }

    /// <summary>
    /// Stage-1 plan approval. The hook intercepts propose_plan and routes here to
    /// render the plan content blocks and read y/n — same channel + pump +
    /// three-outcome machinery, only the rendered body differs.
    /// </summary>
    public Task<bool> RequestPlanAsync(
        IReadOnlyList<string>? issues,
        IReadOnlyList<PlanStep>? steps,
        IReadOnlyList<UserInputNeed>? placeholders,
        IReadOnlyList<ChosenDefault>? defaults,
        CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reg = ct.Register(() => tcs.TrySetCanceled(ct));

        _queue.Writer.TryWrite(new PendingApproval("propose_plan", string.Empty,
            new PlanContent(issues, steps, placeholders, defaults), tcs, reg));
        return tcs.Task;
    }

    /// <summary>
    /// Maps each <see cref="PermissionRequest"/> subtype to a (label, args)
    /// pair the user can read. The fallback returns <see cref="PermissionRequest.Kind"/>
    /// so when the SDK adds a new subtype we still see a recognizable tool name
    /// in the approval prompt — and know exactly which case to add here.
    /// </summary>
    private static (string ToolName, string Args) Describe(PermissionRequest req) => req switch
    {
        PermissionRequestCustomTool c => (c.ToolName,                              c.Args?.ToString() ?? string.Empty),
        PermissionRequestShell s      => ("shell",                                 s.FullCommandText ?? string.Empty),
        PermissionRequestRead r       => ("read",                                  r.Path ?? string.Empty),
        PermissionRequestWrite w      => ("write",                                 w.FileName ?? string.Empty),
        PermissionRequestUrl u        => ("web",                                   u.Url ?? string.Empty),
        PermissionRequestMcp m        => ($"mcp:{m.ServerName}/{m.ToolName}",      m.Args?.ToString() ?? string.Empty),
        PermissionRequestMemory mem   => ($"memory:{mem.Action}",                  $"{mem.Subject}: {mem.Fact}"),
        PermissionRequestHook h       => ($"hook:{h.ToolName}",                    h.ToolArgs?.ToString() ?? string.Empty),
        _                              => (req.Kind ?? "tool",                     string.Empty),
    };

    /// <summary>
    /// Synchronously consumes one pending request (if any) on the calling
    /// thread, writes the prompt + reads y/n from stdin. Designed to be raced
    /// against <c>SendAndWaitAsync</c> via <see cref="Task.WhenAny(Task, Task)"/>
    /// so the host's main thread can pump approvals during a busy turn without
    /// spawning a second stdin reader.
    /// </summary>
    /// <returns>
    /// <see cref="ApprovalOutcome.NotHandled"/> on cancellation; otherwise
    /// <see cref="ApprovalOutcome.Yes"/> / <see cref="ApprovalOutcome.No"/>, or
    /// <see cref="ApprovalOutcome.Redirect(string)"/> when the user typed free
    /// text at the prompt instead of y/n. The SDK tool call is already settled
    /// (true for Yes, false for No/Redirect) before this method returns; the
    /// caller only needs to act on the FeedbackPrompt.
    /// </returns>
    public Task<ApprovalOutcome> PumpOneAsync(CancellationToken ct) => PumpOneAsync(ct, ct);

    /// <param name="queueCt">Stops waiting for a request; does not abandon a read that has started.</param>
    /// <param name="sessionCt">Ends the session; abandons a started read with <see cref="OperationCanceledException"/>.</param>
    public async Task<ApprovalOutcome> PumpOneAsync(CancellationToken queueCt, CancellationToken sessionCt)
    {
        PendingApproval pending;
        try
        {
            pending = await _queue.Reader.ReadAsync(queueCt).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return ApprovalOutcome.NotHandled;
        }

        _spinner?.Pause();
        _abortWatcher?.Pause();
        string? line = null;
        var readCancelled = false;
        try
        {
            Action body = () =>
            {
                Console.WriteLine();
                if (pending.Plan is not null)
                {
                    RenderPlan(pending.Plan);
                    Console.Write("   approve plan? [y]es / [n]o, tell me what to do differently: ");
                }
                else
                {
                    WriteColor(ConsoleColor.DarkYellow, $"⚠ Approve '{pending.ToolName}'?");
                    WriteArgs(pending.ArgsPreview);
                    Console.Write("   [y]es / [n]o, tell me what to do differently: ");
                }
                // Throws OperationCanceledException on session cancel, which also releases the renderer lock.
                line = ConsoleLineInput.ReadLineAsync(sessionCt).GetAwaiter().GetResult();
            };
            if (_renderer is not null) _renderer.RunExclusive(body);
            else                       body();
        }
        catch (OperationCanceledException)
        {
            readCancelled = true;
            // The request token may not be cancelled yet; complete the request so its waiter does not hang.
            pending.Result.TrySetCanceled(sessionCt);
            throw;
        }
        finally
        {
            // After session cancel the abandoned read still owns stdin, so the watcher stays paused.
            if (!readCancelled) _abortWatcher?.Resume();
            _spinner?.Resume();
            pending.Reg.Dispose();
        }

        var trimmed = line?.Trim() ?? string.Empty;
        if (trimmed.Equals("y", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("yes", StringComparison.OrdinalIgnoreCase))
        {
            pending.Result.TrySetResult(true);
            return ApprovalOutcome.Yes;
        }

        // Inverse match: anything that isn't y/yes counts as No. Empty / n / no
        // is a plain rejection; anything else is a rejection PLUS a feedback
        // prompt the host should feed back into the next chat turn.
        pending.Result.TrySetResult(false);
        if (trimmed.Length == 0 ||
            trimmed.Equals("n", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("no", StringComparison.OrdinalIgnoreCase))
            return ApprovalOutcome.No;

        return ApprovalOutcome.Redirect(trimmed);
    }

    // Stage-1 plan rendering: the markdown narrative, then the structured steps
    // as a labeled command list (decision 8 — "Options to fix" style). The
    // numbered [N] select / [esc] cancel affordance is v2; PP approval is the
    // binary y/n prompted by the caller after this renders.
    // REVIEW: raw markdown in a non-UTF-8 PowerShell host may show mojibake for
    // any non-ASCII the model emits — verify in e2e; prompts steer toward ASCII.
    // Stage-1 plan rendering for the inline console. The model writes planMd as
    // markdown, but the inline console can't render it — so we strip markdown to
    // readable plain text (headings become spaced lines, **bold**/`code` lose
    // their markers). A TUI front-end would render the markdown directly instead.
    // Renders the plan as styled content blocks (the unified display system).
    // Color is minimal & theme-safe: only Red (danger) and DarkYellow (warning)
    // are explicit; everything else uses the terminal's default foreground so it
    // stays readable on light AND dark themes (white-family colors would vanish on
    // a light background). Headings are distinguished by structure, not color.
    private static void RenderPlan(PlanContent plan)
    {
        Console.WriteLine("AI help");
        Console.WriteLine("───────");

        if (plan.Issues is { Count: > 0 })
        {
            // No blank line between the "AI help" header and "What went wrong:" (wzl).
            Console.WriteLine("What went wrong:");
            foreach (var issue in plan.Issues)
                Console.WriteLine($"• {OneLine(issue)}");
        }

        if (plan.Steps is { Count: > 0 })
        {
            Console.WriteLine();
            // Singular when there's one step, plural for two or more (wzl).
            Console.WriteLine(plan.Steps.Count == 1 ? "Next action:" : "Next actions:");
            var first = true;
            foreach (var step in plan.Steps)
            {
                if (!first) Console.WriteLine();   // blank line between actions
                first = false;
                if (step.Destructive)
                    WriteColor(ConsoleColor.Red, $"⚠ {step.Id}. {OneLine(step.Label)}");
                else
                    Console.WriteLine($"{step.Id}. {OneLine(step.Label)}");
                if (!string.IsNullOrWhiteSpace(step.Cmd))
                    Console.WriteLine($"└─ {OneLine(step.Cmd)}");
            }
        }

        RenderReviewValues(plan.Steps, plan.Defaults, plan.Placeholders);
        // Blank line between the review-values block and the disclaimer, then the
        // disclaimer as the last line of every rendered plan — so it travels with
        // the plan wherever it's shown (one-shot at submit, interactive before
        // approval) via this single path.
        Console.WriteLine();
        Console.WriteLine(Disclaimer);
        Console.WriteLine();
    }

    // Soft-wrap long args across terminal width. Never truncates — the user
    // needs to see the FULL command (passwords, unusual flags hide at the
    // tail). Wraps at whitespace so a whitespace-delimited token isn't cut
    // mid-word; a single token longer than the line is hard-split as a last
    // resort. Note: this is not shell-quote-aware — spaces inside a quoted
    // value (e.g. --query "foo bar") are still treated as wrap points, since
    // faithfully parsing quotes/escapes across bash+powershell isn't worth it
    // for a display-only wrap. Continuation lines align under the first arg.
    private const string ArgsLabel    = "   args: ";
    private const string ArgsContinue = "         ";
    private static void WriteArgs(string args)
    {
        if (string.IsNullOrWhiteSpace(args))
        {
            Console.WriteLine(ArgsLabel);
            return;
        }
        int width = TryGetWindowWidth();
        int budget = Math.Max(20, width - ArgsLabel.Length);
        bool first = true;
        foreach (var line in WrapAtWhitespace(args, budget))
        {
            Console.Write(first ? ArgsLabel : ArgsContinue);
            Console.WriteLine(line);
            first = false;
        }
    }

    // Greedy word-wrap: packs whitespace-delimited tokens onto lines up to
    // budget, breaking a token across lines only when it alone exceeds budget.
    private static IEnumerable<string> WrapAtWhitespace(string text, int budget)
    {
        var line = new System.Text.StringBuilder();
        foreach (var token in SplitKeepingWidth(text, budget))
        {
            if (line.Length == 0)
            {
                line.Append(token);
            }
            else if (line.Length + 1 + token.Length <= budget)
            {
                line.Append(' ').Append(token);
            }
            else
            {
                yield return line.ToString();
                line.Clear();
                line.Append(token);
            }
        }
        if (line.Length > 0)
            yield return line.ToString();
    }

    // Splits on whitespace; any token longer than budget is chunked so the
    // greedy packer above never receives a token it can't place.
    private static IEnumerable<string> SplitKeepingWidth(string text, int budget)
    {
        foreach (var token in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.Length <= budget)
            {
                yield return token;
                continue;
            }
            for (int i = 0; i < token.Length; i += budget)
                yield return token.Substring(i, Math.Min(budget, token.Length - i));
        }
    }

    private static int TryGetWindowWidth()
    {
        try
        {
            int w = Console.WindowWidth;
            return w > 0 ? w : 120;
        }
        catch
        {
            return 120; // redirected stdout / non-TTY
        }
    }

    // Color is for interactive terminals only. In CI / a pipe / redirected I/O we
    // emit plain text so escape codes don't pollute logs.
    private static readonly bool s_useColor = ConsoleEnvironment.IsInteractive;

    private static void WriteColor(ConsoleColor color, string text)
    {
        if (!s_useColor) { Console.WriteLine(text); return; }
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

    private static void WriteColorInline(ConsoleColor color, string text)
    {
        if (!s_useColor) { Console.Write(text); return; }
        var prev = Console.ForegroundColor;
        try
        {
            Console.ForegroundColor = color;
            Console.Write(text);
        }
        finally
        {
            Console.ForegroundColor = prev;
        }
    }
}
