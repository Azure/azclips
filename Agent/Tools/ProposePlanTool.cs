using System.ComponentModel;
using Core.Agent.Tools.Cache;
using Microsoft.Extensions.AI;

namespace Core.Agent.Tools;

/// <summary>
/// The plan-proposal tool. Before the agent runs commands to fix a failed
/// operation, it calls propose_plan with a human-readable markdown plan plus a
/// structured step list. The host intercepts the call in OnPreToolUse, renders
/// the markdown for the user, and asks for direction approval (stage 1). The
/// individual commands still get their own per-command approval later (stage 2).
///
/// Two-layer args by design:
///   • planMd — the narrative the user reads (what we'll do and why)
///   • steps  — structured commands the host uses to label destructive ops,
///              show exact cmds at stage-2 confirm, and emit telemetry
/// Both are required; they describe the same plan from the user's vs the host's
/// point of view. Modeled on Claude Code's ExitPlanMode (tool call + ask
/// permission), not a bespoke event. Argument validation lives in the hook, so
/// this body only logs.
/// </summary>
public sealed class ProposePlanTool
{
    public ProposePlanTool()
    {
        ProposePlan = AIFunctionFactory.Create(ProposePlanImpl, new AIFunctionFactoryOptions
        {
            Name = "propose_plan",
            Description =
                "Propose a plan for the user to approve before you run any commands. " +
                "Call this when a command failed and you have a multi-step fix, or whenever " +
                "you want the user to confirm direction before execution. " +
                "planMd is the markdown the user reads; steps is the structured command list " +
                "(each command is shown again for per-command approval before it runs). " +
                "After the user approves the plan, proceed to run the steps.",
        });

        All = new[] { ProposePlan };
    }

    public AIFunction ProposePlan { get; }
    public IReadOnlyList<AIFunction> All { get; }

    [Description("Propose a plan for user approval before running commands.")]
    private static string ProposePlanImpl(
        [Description("The plan as markdown — what you'll do and why. This is what the user reads. Required.")]
        string planMd,
        [Description("Structured steps. Each: id (1-based), label (short title), cmd (exact command), destructive (set true for irreversible ops — delete/remove/purge; the host also auto-flags known danger verbs), rationale (optional one-line why, shown under the command). At least one step.")]
        IReadOnlyList<StepMeta> steps,
        // TODO v2: wire to telemetry (correlate which error codes trigger plans).
        [Description("Reserved for future use. Pass null.")]
        string? triggerErrorCode = null)
    {
        using var scope = ToolInvocationLog.Start("propose_plan", $"steps={steps?.Count ?? 0}");
        scope.SetResult($"plan proposed ({steps?.Count ?? 0} steps)");
        return "plan submitted for user review";
    }
}

/// <summary>
/// One step of a proposed plan. <see cref="Destructive"/> is nullable so the LLM
/// can omit it; the host marks a step destructive when this is true OR the command
/// contains a danger verb, so an omission still gets flagged by the host backstop.
/// </summary>
public sealed record StepMeta(
    int Id,
    string Label,
    string Cmd,
    bool? Destructive = null,
    string? Rationale = null);
