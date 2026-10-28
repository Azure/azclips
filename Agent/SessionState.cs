using Core.Agent.Hooks;
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

    // oneshot: single turn; the first valid propose_plan is shown and ends the
    // session (submit_response still closes turns without a plan). interactive:
    // plan is approved via y/n and execution proceeds, then submit_response closes.
    public bool IsOneShot { get; set; }

    // True for the Azure CLI flavor, where every plan step that calls azclips must
    // start with `az`; false for the PowerShell flavor (steps are cmdlets).
    public bool IsCliFlavor { get; set; }

    // Reads the live help catalog as JSON (`az agent tool help`, the list_commands
    // source). The plan hook uses it to tell an azclips command that lost its `az`
    // from prose or another program. Null when none is wired: no step changes.
    public Func<CancellationToken, Task<string>>? ReadCommandCatalog { get; set; }

    // The CLI command names from that catalog. Read at most once per session, on the
    // first plan with a step that does not start with `az`; empty when the lookup failed.
    public IReadOnlyList<string>? CommandNames { get; set; }

    // Set when any plan was rendered this session. Informational only.
    public bool PlanShown { get; set; }

    // The plan content from the most recent propose_plan, rendered with the final
    // outcome (right away in one-shot, by the next submit_response in interactive).
    // (one-shot also re-shows these on exit as a fallback if nothing rendered them.)
    public IReadOnlyList<string>? LastIssues { get; set; }
    public IReadOnlyList<ConsoleApprovalPrompt.PlanStep>? LastPlanSteps { get; set; }
    public IReadOnlyList<ConsoleApprovalPrompt.UserInputNeed>? LastNeedsUserInput { get; set; }
    public IReadOnlyList<ConsoleApprovalPrompt.ChosenDefault>? LastDefaults { get; set; }

    // Invoked right after the final outcome is rendered. Lets the renderer
    // (TraceAgentRenderer) flip into "suppress trailing prose" mode so the model
    // can't slip a stray paragraph past the terminator. Null = no suppression wired
    // (non-trace renderers).
    public Action? OnFinalRendered { get; set; }

    // Invoked right after the final outcome is rendered, with the plan steps exactly
    // as shown (null when the outcome has no plan). The host records them in the
    // diagnostic trace so evaluations grade what the user saw.
    public Action<IReadOnlyList<ConsoleApprovalPrompt.PlanStep>?>? OnOutcomeShown { get; set; }

    // Set once the host has rendered a final outcome. The host reads it to
    // recognize the session terminated cleanly.
    public bool SubmitRendered { get; set; }

    // True once the final outcome has rendered THIS turn. Reset at the start of every
    // SendAsync. The pre-tool-use hook denies any further tool call once it's set,
    // so the model can't keep acting after the terminator — without cancelling the
    // turn (cancellation surfaces as OperationCanceledException, misread as Ctrl+C).
    public bool SubmittedThisTurn { get; set; }

    // Files that the failed command names, such as a --template-file. Reading one needs no approval.
    public FailedCommandFiles? FailedCommandFiles { get; set; }
}
