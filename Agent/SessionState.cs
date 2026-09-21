using Core.Agent.Hosting;

namespace Core.Agent;

/// <summary>
/// Per-session mutable state shared with the OnPreToolUse hook. Holds the plan
/// gate and the turn-scoped cancellation source; it is session-scoped (one
/// instance per ChatAgent session) and not thread-safe by design — the SDK
/// invokes the hook serially per session.
/// </summary>
internal sealed class SessionState
{
    // True once the user has approved any plan this session. Gates bash/powershell.
    public bool PlanApproved { get; set; }

    // oneshot: single turn, plan then submit_response closes. interactive: plan is
    // approved via y/n and execution proceeds, then submit_response closes.
    public bool IsOneShot { get; set; }
    public bool IsManualRecovery { get; set; }
    public IReadOnlySet<string> AllowedManualTools { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    // Set when any plan was rendered this session. Informational only —
    // submit_response is the sole terminator, so this no longer ends the turn.
    public bool PlanShown { get; set; }

    // The plan content from the most recent propose_plan, attached to the next
    // submit_response render so the plan's content blocks show together.
    // (one-shot also re-shows these on exit as a fallback if submit never ran.)
    public IReadOnlyList<string>? LastIssues { get; set; }
    public IReadOnlyList<ConsoleApprovalPrompt.PlanStep>? LastPlanSteps { get; set; }
    public IReadOnlyList<ConsoleApprovalPrompt.UserInputNeed>? LastNeedsUserInput { get; set; }
    public IReadOnlyList<ConsoleApprovalPrompt.ChosenDefault>? LastDefaults { get; set; }

    // Invoked by the submit_response hook right after the final outcome is
    // rendered. Lets the renderer (TraceAgentRenderer) flip into "suppress
    // trailing prose" mode so the model can't slip a stray paragraph past the
    // terminator. Null = no suppression wired (non-trace renderers).
    public Action? OnFinalRendered { get; set; }

    // Set once the host has rendered a submit_response outcome. The host reads it
    // to recognize the session terminated cleanly via submit_response.
    public bool SubmitRendered { get; set; }

    // True once submit_response has rendered THIS turn. Reset at the start of every
    // SendAsync. The pre-tool-use hook denies any further tool call once it's set,
    // so the model can't keep acting after the terminator — without cancelling the
    // turn (cancellation surfaces as OperationCanceledException, misread as Ctrl+C).
    public bool SubmittedThisTurn { get; set; }
}
