using System.ComponentModel;
using Core.Agent.Tools.Resource.Cache;
using Microsoft.Extensions.AI;

namespace Core.Agent.Hosting;

/// <summary>
/// The plan-proposal tool. Before the agent runs commands to fix a failed
/// operation, it calls propose_plan with a structured fix. The host intercepts
/// the call in OnPreToolUse, renders it for the user, and asks for direction
/// approval (stage 1). Individual commands still get per-command approval (stage 2).
///
/// Structured by design (no free-form markdown) so the host renders each part as
/// its own styled content block (see the "unified display system" design):
///   • issues       — what went wrong, as plain-text points
///   • steps         — the ordered commands to fix it
///   • placeholders  — secrets only the user knows (rendered with &lt;name&gt;)
///   • defaults      — values the agent chose/derived (rendered as the real value)
/// Argument validation lives in the hook, so this body only logs.
/// </summary>
public sealed class ProposePlanTool
{
    public ProposePlanTool()
    {
        ProposePlan = AIFunctionFactory.Create(ProposePlanImpl, new AIFunctionFactoryOptions
        {
            Name = "propose_plan",
            Description =
                "Propose a fix for the user to review before any command runs. Call this when a " +
                "command failed and you have a fix, or whenever you want the user to confirm " +
                "direction before execution. Fields: " +
                "issues (what went wrong, as short points), " +
                "steps (the commands to fix it, in order), " +
                "placeholders (secrets only the user can supply; optional), " +
                "defaults (values you chose or derived for the user; optional).",
        });

        All = new[] { ProposePlan };
    }

    public AIFunction ProposePlan { get; }
    public IReadOnlyList<AIFunction> All { get; }

    [Description("Propose a fix (issues + steps) for user review before running commands.")]
    private static string ProposePlanImpl(
        [Description("What went wrong: a list of short plain-text points, one string per point. Each point states one reason the command failed or one fact the user should know (e.g. \"The -g short flag isn't supported — use --resource-group\"). At least one point.")]
        IReadOnlyList<string> issues,
        [Description("The commands that fix the problem, in order. Each: id (1-based), label (a short sentence describing this step), cmd (the exact command), destructive (set true for irreversible ops — delete/remove/purge; danger verbs auto-flag too). At least one step.")]
        IReadOnlyList<StepMeta> steps,
        [Description("Optional. Secrets only the user knows — a password, an SSH private key, anything the system can't obtain or infer. Each: name (the flag name without dashes, e.g. \"admin-password\") + description (one short phrase telling the user what to fill in). Use <name> as a placeholder in the command (e.g. --admin-password <admin-password>); not replacing it is expected to fail. Do NOT list values the environment can supply (e.g. subscription id from the signed-in context) — those go in defaults. Omit when there are none.")]
        IReadOnlyList<UserInputNeed>? placeholders = null,
        [Description("Optional. Values you chose or derived for the user — a default region/size, a subscription id from the signed-in context, or names derived from the user's input (e.g. myvm-nic / myvm-vnet). Each: name (the flag name without dashes), value (the actual value you put in the command), note (one short phrase, why you picked it). Put the real value in the command so the user can run it as-is; this just flags it so they can change it. Do NOT list values the user already wrote in their original command. Omit when there are none.")]
        IReadOnlyList<ChosenDefault>? defaults = null)
    {
        using var scope = ToolInvocationLog.Start("propose_plan",
            $"issues={issues?.Count ?? 0} steps={steps?.Count ?? 0} placeholders={placeholders?.Count ?? 0} defaults={defaults?.Count ?? 0}");
        scope.SetResult($"plan proposed ({issues?.Count ?? 0} issues, {steps?.Count ?? 0} steps)");
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
    bool? Destructive = null);

/// <summary>
/// A secret only the user can supply (password, SSH key, …). The agent uses
/// <c>&lt;Name&gt;</c> as a placeholder in the command; the host renders it as a
/// fill-in reminder. Not replacing it is expected to fail.
/// </summary>
public sealed record UserInputNeed(
    string Name,
    string Description);

/// <summary>
/// A value the agent chose or derived for the user (default region/size, a
/// probe-supplied subscription id, a derived resource name). The real value is in
/// the command so it runs as-is; the host flags it so the user can change it.
/// </summary>
public sealed record ChosenDefault(
    string Name,
    string Value,
    string Note);
