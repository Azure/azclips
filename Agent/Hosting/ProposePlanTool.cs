using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
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
///   • defaults      — flags the agent chose/derived (rendered from the command's real token)
/// The schema advertises non-empty issues and steps; the hook still validates
/// the full plan before approval. This body only logs.
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
                "placeholders (secret flags - a password, an SSH private key, only the user can supply; optional), " +
                "defaults (non-secret flags not in the user's original command — a value you chose, a name you derived, or a valueless switch you supplied; optional).",
        });

        All = new[] { ProposePlan };
    }

    public AIFunction ProposePlan { get; }
    public IReadOnlyList<AIFunction> All { get; }

    [Description("Propose a fix (issues + steps) for user review before running commands.")]
    private static string ProposePlanImpl(
        [Description("What went wrong: a list of short plain-text points, one string per point. Each point states one reason the command failed or one fact the user should know (e.g. \"The -g short flag isn't supported — use --resource-group\").")]
        [MinLength(1)]
        IReadOnlyList<string> issues,
        [Description("The commands that fix the problem, in order. Each: id (1-based), label (a short sentence describing this step), cmd (the exact command: (1) use <flag-name> as a placeholder in the parameter value for secret flags whose value you don't know, e.g. --admin-password <admin-password>; (2) put the real parameter value in the cmd for a value you chose (a common region, a moderate VM size) or a name you derived, e.g. `<vm>-nic`, `<vm>-vnet`, `<vm>-nsg`.), destructive (set true for irreversible ops — delete/remove/purge; danger verbs auto-flag too).")]
        [MinLength(1)]
        IReadOnlyList<StepMeta> steps,
        [Description("Optional. Secret flags only the user knows — a password, an SSH private key, anything the system can't obtain or infer. Each: flag (as written in your step's cmd, e.g. --admin-password) + description (one short phrase telling the user what to fill in). List flags you added or modified; don't list a flag the user already filled in themselves.")]
        IReadOnlyList<UserInputNeed>? placeholders = null,
        [Description("Optional. Non-secret flags not in the user's original command — a value you chose (a default region/size, a subscription id from the signed-in context), a name you derived (e.g. myvm-nic / myvm-vnet), or a valueless switch you supplied (e.g. --active). Each: flag (as written in your step's cmd, e.g. --location), note (one short phrase, why you added it). Don't include the value — we read it from the matching flag in your step's cmd. List flags you added or modified; don't list a flag the user already filled in themselves.")]
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
/// A secret only the user can supply (password, SSH key, …). This record just
/// names the flag (e.g. <c>--admin-password</c>); the agent writes that flag with a
/// <c>&lt;placeholder&gt;</c> value in the step command. The host looks the flag up in
/// the step command to render a fill-in reminder. Not replacing it is expected to fail.
/// </summary>
public sealed record UserInputNeed(
    string Flag,
    string Description);

/// <summary>
/// A flag the agent chose or derived for the user (default region/size, a
/// probe-supplied subscription id, a derived resource name). The value lives in
/// the step command; the host reads it from there and flags it for review, so
/// the display always matches what runs.
/// </summary>
public sealed record ChosenDefault(
    string Flag,
    string Note);
