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
            return new PreToolUseHookOutput
            {
                PermissionDecision = "deny",
                PermissionDecisionReason = "This turn already ended with submit_response.",
            };

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
            approvalSink.ShowFinalOutcome(
                status,
                message,
                state.LastIssues,
                state.LastPlanSteps,
                state.LastNeedsUserInput,
                state.LastDefaults,
                state.IsOneShot);
            state.SubmitRendered = true;
            // Mark the turn as ended so the post-terminator gate (top of this hook)
            // denies any further tool call. Tell the renderer the final outcome is on
            // screen so it swallows trailing prose the model may still stream. We do
            // NOT cancel the turn — cancellation surfaces as OperationCanceledException
            // and is misread as Ctrl+C; the gate + renderer suppression are enough.
            state.SubmittedThisTurn = true;
            state.OnFinalRendered?.Invoke();
            // Consume the plan + needs so the next turn (interactive only) starts
            // fresh — if the agent proposes again, state will be repopulated; if it
            // does not, the next submit_response won't redundantly re-render them.
            state.LastIssues = null;
            state.LastPlanSteps = null;
            state.LastNeedsUserInput = null;
            state.LastDefaults = null;
            return new PreToolUseHookOutput { PermissionDecision = "allow" };
        }

        // propose_plan is a mid-turn artifact — it renders a plan and never ends
        // the session (submit_response does). Validate args (deny + correction on
        // failure), then render. oneshot shows the plan and allows; interactive
        // asks y/n and, on yes, unblocks execution. Either way the agent continues
        // and must call submit_response to close.
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

            // Unified mind: propose_plan does NOT render. submit_response is the
            // sole renderer — it shows the plan's content blocks together, then
            // closes. In interactive mode the host loop keeps going so the user
            // can answer the needs in their next message.
            if (state.IsOneShot)
                return new PreToolUseHookOutput { PermissionDecision = "allow" };

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

    // A turn-closing tool satisfies the act-through-tools contract: propose_plan
    // (renders a plan) or submit_response (ends the session). A turn that only
    // queried (get_command/bash/find_*) then replied in prose is still a violation.
    //
    // In one-shot mode propose_plan does NOT count as closing: the agent must go on
    // to call submit_response to render the plan, so a propose-only turn should be
    // nudged forward, not let through (otherwise the plan is never shown — it lives
    // in LastPlanMarkdown with nobody to render it). In interactive mode propose_plan
    // DOES count (it stops for y/n approval, which is the expected pause).
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
