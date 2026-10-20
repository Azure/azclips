using System.Text.Json;
using Core.Agent.Common;
using Core.Agent.Hosting;
using Core.Agent.Plan;
using GitHub.Copilot;
using GitHub.Copilot.Rpc;

namespace Core.Agent.Hooks;

/// <summary>
/// PreToolUse hook logic — the SDK calls into this before every tool
/// invocation, and this class decides allow/deny/rewrite. Kept out of
/// <see cref="ChatAgent"/> so the hook (plan-approval gate, submit_response
/// terminator, az command rewrite) can be reviewed and tested in isolation
/// from the session lifecycle.
/// </summary>
internal static class PreToolUseHook
{
    internal static async Task<PreToolUseHookOutput?> HandleAsync(
        PreToolUseHookInput input,
        string azExePath,
        ConsoleApprovalPrompt approvalSink,
        SessionState state,
        CancellationToken ct)
    {
        // Post-terminator gate: once submit_response has rendered this turn, the
        // turn is logically over — deny any further tool call instead of cancelling
        // the turn (cancellation would surface as OperationCanceledException, which
        // the host misreads as Ctrl+C). Trailing prose is suppressed renderer-side.
        if (state.SubmittedThisTurn)
            return TurnEndedDenial();

        // submit_response is the one and only session terminator (unified mind).
        // No user approval — the LLM already decided to end. Validate args; on
        // failure deny + feed the reason back as additionalContext so the LLM
        // self-corrects. On success render the final outcome and allow: the host
        // recognizes this turn (via SubmitRendered) as the clean close.
        if (string.Equals(input.ToolName, "submit_response", StringComparison.OrdinalIgnoreCase))
        {
            var submitError = PlanArgsParser.ValidateSubmitResponse(input.ToolArgs);
            if (submitError is not null)
                return new PreToolUseHookOutput
                {
                    PermissionDecision = "deny",
                    PermissionDecisionReason = $"Invalid submit_response arguments: {submitError}",
                    AdditionalContext = $"Your submit_response call was rejected: {submitError}. Call it again with corrected arguments.",
                };

            var status = ArgsNormalizer.TryGetString(input.ToolArgs, "status") ?? string.Empty;
            var message = ArgsNormalizer.TryGetString(input.ToolArgs, "message");
            // submit_response is the unified renderer: it shows the plan the agent
            // proposed earlier this turn (if any) as content blocks, then the status.
            // interactive will keep looping; one-shot exits.
            ShowFinalOutcome(approvalSink, state, status, message);
            return new PreToolUseHookOutput { PermissionDecision = "allow" };
        }

        // propose_plan validates and stores the plan. One-shot shows it right away and
        // ends the turn (propose_plan is a terminal tool there, see ChatAgent), because a
        // second model turn only to call submit_response adds seconds and never changed
        // the plan. Interactive asks y/n and, on yes, unblocks execution; that agent then
        // continues and calls submit_response to close.
        if (string.Equals(input.ToolName, "propose_plan", StringComparison.OrdinalIgnoreCase))
        {
            var planError = PlanArgsParser.ValidateProposePlan(input.ToolArgs);
            if (planError is not null)
                return new PreToolUseHookOutput
                {
                    PermissionDecision = "deny",
                    PermissionDecisionReason = $"Invalid propose_plan arguments: {planError}",
                    AdditionalContext = $"Your propose_plan call was rejected: {planError}. Call it again with corrected arguments.",
                };

            var issues = PlanArgsParser.ExtractIssues(input.ToolArgs);
            var planSteps = PlanArgsParser.ExtractPlanSteps(input.ToolArgs);
            if (state.IsCliFlavor && PlanCommandNormalizer.NeedsCatalog(planSteps))
            {
                state.CommandNames ??= await ReadCommandNamesAsync(state, ct).ConfigureAwait(false);
                planSteps = PlanCommandNormalizer.EnsureAzPrefix(planSteps, state.CommandNames);
                // Another closing hook may have ended the turn during the lookup.
                if (state.SubmittedThisTurn)
                    return TurnEndedDenial();
            }
            var placeholders = PlanArgsParser.ExtractUserInputNeeds(input.ToolArgs, "placeholders");
            var defaults = PlanArgsParser.ExtractChosenDefaults(input.ToolArgs);
            state.LastIssues = issues;
            state.LastPlanSteps = planSteps;
            state.LastNeedsUserInput = placeholders;
            state.LastDefaults = defaults;
            state.PlanShown = true;

            // TODO (deferred from batch B): host-side required-parameter validation.
            // A RequiredParameterValidator (mirror of ExampleSyntaxValidator, driven
            // by CommandMetadata.IsRequired) would deny + force a re-plan when a step
            // omits a required flag. Deferred: it needs a command resolver
            // (IExtensionIndex) threaded into this static hook plus an Agent ->
            // Azclips.Help.Tool reference that does not exist today; wiring both here
            // is out of scope for this batch. Draft lives in the v3 review doc.

            // One-shot: render exactly what submit_response would render for a
            // completed plan, then let the terminal propose_plan call end the turn.
            if (state.IsOneShot)
            {
                ShowFinalOutcome(approvalSink, state, "completed", message: null);
                return new PreToolUseHookOutput { PermissionDecision = "allow" };
            }

            var planApproved = await approvalSink.RequestPlanAsync(issues, planSteps, placeholders, defaults, ct).ConfigureAwait(false);
            if (planApproved)
            {
                // Session-level gate: once any plan is approved, execution is
                // unblocked for the rest of the session (stage-2 per-command
                // approval still applies).
                state.PlanApproved = true;
                // Tell the model what to do next. propose_plan returns a terse
                // "submitted for review" string, and an "allow" alone doesn't
                // signal that approval happened — so without this the model often
                // stops instead of executing. Mirrors the reject branch, which
                // also steers the next turn via AdditionalContext. one-shot never
                // reaches here (it returned above); this only drives interactive.
                return new PreToolUseHookOutput
                {
                    PermissionDecision = "allow",
                    AdditionalContext = "The user approved the plan. Now carry it out: run its steps in order with bash/powershell (each command is confirmed once more before it runs), adapt if a command fails, and call submit_response when the task is done.",
                };
            }

            // Rejected plan = the user wants a different plan, not a bare-prose
            // correction. Roll the gate back so the agent must re-propose, and tell
            // it to ask what to change (interactive can wait for the answer; in
            // one-shot the turn simply ends with no approved plan).
            state.PlanApproved = false;
            return new PreToolUseHookOutput
            {
                PermissionDecision = "deny",
                PermissionDecisionReason = "User rejected the plan.",
                AdditionalContext = "The user rejected this plan. Ask them what to change, then propose a revised plan — do not run any command and do not reply with prose only.",
            };
        }

        // Plan-before-execute gate: the agent must get a plan approved before it
        // runs any shell command. Without it, deny and tell the LLM to call
        // propose_plan first (round-trips via additionalContext).
        if ((string.Equals(input.ToolName, "bash",       StringComparison.OrdinalIgnoreCase) ||
             string.Equals(input.ToolName, "powershell", StringComparison.OrdinalIgnoreCase)) &&
            !state.PlanApproved)
        {
            return new PreToolUseHookOutput
            {
                PermissionDecision = "deny",
                PermissionDecisionReason = "Execution requires an approved plan first.",
                AdditionalContext = "You must call propose_plan and get the user's approval before running any command.",
            };
        }

        // Both built-in shell tools take a "command" arg and may contain a bare
        // "az" that PowerShell would resolve via PATH. Rewrite to the absolute
        // az.exe path so the agent always invokes the azclips binary that
        // launched it, regardless of what else is on PATH.
        if (!string.Equals(input.ToolName, "bash",       StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(input.ToolName, "powershell", StringComparison.OrdinalIgnoreCase))
            return null;

        var command = ExtractBashCommand(input.ToolArgs);
        if (command is null) return null;

        if (PlaceholderDetector.TryDetect(command, out var placeholder))
        {
            return new PreToolUseHookOutput
            {
                PermissionDecision = "deny",
                PermissionDecisionReason = $"Detected placeholder '{placeholder}' in command. Ask the user for the real value.",
            };
        }

        var rewritten = RewriteAz(command, azExePath);
        if (ReferenceEquals(rewritten, command))
            return null;

        // SDK semantics: "allow" honors ModifiedArgs but skips OnPermissionRequest;
        // "ask" prompts via OnPermissionRequest but discards ModifiedArgs. To get
        // both rewrite AND user approval, take the approval here, then "allow".
        var approved = await approvalSink.RequestRewriteAsync(input.ToolName!, rewritten, ct).ConfigureAwait(false);
        if (!approved)
        {
            return new PreToolUseHookOutput
            {
                PermissionDecision = "deny",
                PermissionDecisionReason = "User rejected the rewritten az command.",
            };
        }

        return new PreToolUseHookOutput
        {
            PermissionDecision = "allow",
            ModifiedArgs = MergeCommand(input.ToolArgs, rewritten),
        };
    }

    // ModifiedArgs replaces the tool args wholesale, so preserve every original
    // field (e.g. powershell's required "description") and overwrite only "command".
    private static Dictionary<string, object?> MergeCommand(object? originalArgs, string rewrittenCommand)
    {
        var merged = new Dictionary<string, object?>();
        if (originalArgs is JsonElement je)
        {
            JsonDocument? parsedDoc = null;
            try
            {
                if (je.ValueKind == JsonValueKind.String)
                {
                    try { parsedDoc = JsonDocument.Parse(je.GetString()!); je = parsedDoc.RootElement; }
                    catch { /* fall through with empty merged */ }
                }
                if (je.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in je.EnumerateObject())
                        merged[prop.Name] = prop.Value.Clone();
                }
            }
            finally
            {
                parsedDoc?.Dispose();
            }
        }
        else if (originalArgs is IDictionary<string, object?> dict)
        {
            foreach (var kv in dict) merged[kv.Key] = kv.Value;
        }
        merged["command"] = rewrittenCommand;
        return merged;
    }

    // Reads the CLI command names from the live catalog for the az-prefix rule. A failed
    // lookup gives no names, so no step changes; the result is kept for the session.
    private static async Task<IReadOnlyList<string>> ReadCommandNamesAsync(SessionState state, CancellationToken ct)
    {
        if (state.ReadCommandCatalog is null) return [];
        try
        {
            var catalog = await state.ReadCommandCatalog(ct).ConfigureAwait(false);
            return PlanCommandNormalizer.ReadCommandNames(catalog) ?? [];
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            return [];
        }
    }

    // Renders the final outcome with the stored plan (if any) and marks the turn as
    // ended: the post-terminator gate (top of HandleAsync) then denies any further
    // tool call, and the renderer swallows trailing prose. We do NOT cancel the
    // turn — cancellation surfaces as OperationCanceledException and is misread as
    // Ctrl+C. The plan is consumed so a later interactive turn starts fresh.
    private static PreToolUseHookOutput TurnEndedDenial() => new()
    {
        PermissionDecision = "deny",
        PermissionDecisionReason = "This turn already ended: its final outcome was shown (by submit_response or a one-shot plan).",
    };

    private static void ShowFinalOutcome(ConsoleApprovalPrompt approvalSink, SessionState state, string status, string? message)
    {
        approvalSink.ShowFinalOutcome(
            status,
            message,
            state.LastIssues,
            state.LastPlanSteps,
            state.LastNeedsUserInput,
            state.LastDefaults,
            state.IsOneShot);
        state.SubmitRendered = true;
        state.SubmittedThisTurn = true;
        state.OnOutcomeShown?.Invoke(state.LastPlanSteps);
        state.OnFinalRendered?.Invoke();
        state.LastIssues = null;
        state.LastPlanSteps = null;
        state.LastNeedsUserInput = null;
        state.LastDefaults = null;
    }

    // A turn-closing tool satisfies the act-through-tools contract: propose_plan
    // (renders a plan) or submit_response (ends the session). A turn that only
    // queried (get_command/bash/find_*) then replied in prose is still a violation.
    //
    // In one-shot mode propose_plan does NOT count here: a valid one-shot plan is
    // rendered by the hook and closes the session (ChatAgent.SessionClosed, which the
    // host checks first), while a rejected one must still be nudged forward. In
    // interactive mode propose_plan DOES count (it stops for y/n approval, which is
    // the expected pause).
    // internal for unit testing (see ClosingToolPredicateTests).
    internal static bool IsClosingTool(string? toolName, bool isOneShot = false) =>
        IsSessionTerminator(toolName) ||
        (!isOneShot && string.Equals(toolName, "propose_plan", StringComparison.OrdinalIgnoreCase));

    // The session terminator under the unified-mind model: only submit_response
    // ends a session. propose_plan is a mid-turn artifact and is NOT a terminator.
    // internal for unit testing (see ClosingToolPredicateTests).
    internal static bool IsSessionTerminator(string? toolName) =>
        string.Equals(toolName, "submit_response", StringComparison.OrdinalIgnoreCase);

    internal static bool IsReadOnlyCacheTool(string toolName) =>
        toolName.StartsWith("find_", StringComparison.OrdinalIgnoreCase) ||
        toolName.StartsWith("get_",  StringComparison.OrdinalIgnoreCase) ||
        toolName.StartsWith("list_", StringComparison.OrdinalIgnoreCase);

    private static string? ExtractBashCommand(object? args)
    {
        if (args is null) return null;

        // SDK may hand us the args as either a JSON object (normal case) or a
        // JSON-encoded string of an object (observed for the built-in
        // powershell tool). Handle both shapes uniformly.
        if (args is JsonElement je)
        {
            JsonDocument? parsedDoc = null;
            try
            {
                if (je.ValueKind == JsonValueKind.String)
                {
                    try { parsedDoc = JsonDocument.Parse(je.GetString()!); je = parsedDoc.RootElement; }
                    catch { return null; }
                }
                if (je.ValueKind == JsonValueKind.Object &&
                    je.TryGetProperty("command", out var cmd) && cmd.ValueKind == JsonValueKind.String)
                    return cmd.GetString();
                return null;
            }
            finally
            {
                parsedDoc?.Dispose();
            }
        }
        if (args is IDictionary<string, object?> dict && dict.TryGetValue("command", out var v))
            return v?.ToString();
        return null;
    }

    private static string RewriteAz(string command, string azExePath)
    {
        var trimmed = command.TrimStart();
        if (!trimmed.StartsWith("az ", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.Equals("az", StringComparison.OrdinalIgnoreCase))
            return command;

        var rest = trimmed.Length > 2 ? trimmed[3..] : string.Empty;
        var quoted = azExePath.Contains(' ') ? $"\"{azExePath}\"" : azExePath;
        return string.IsNullOrEmpty(rest) ? quoted : $"{quoted} {rest}";
    }
}
