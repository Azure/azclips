// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;
using Core.Agent.Hosting;

namespace Core.Agent.Plan
{
    /// <summary>
    /// Deterministic, rule-driven post-processing for a proposed plan, run right
    /// before it is rendered. The LLM's structured output is a context-based
    /// prediction and occasionally mis-shapes a field; rather than pile more
    /// instructions into the prompt (which slows the agent and can't be
    /// exhaustive), we correct a small set of well-understood surface issues here.
    ///
    /// Rules are PRECISE on purpose. Each rule may require a specific command to
    /// be present in the plan before it touches anything, so a fix for one command
    /// can never misfire on another command that happens to share a flag name
    /// (e.g. dropping "true" on `profile show --active` must not affect a valued
    /// boolean like `config set --value true`). Rules live in one table
    /// (<see cref="Rules"/>); add a cleanup by adding a rule, nothing else changes.
    /// </summary>
    public static class PlanSanitizer
    {
        private static readonly IReadOnlyList<SanitizeRule> Rules = new SanitizeRule[]
        {
            // `az profile show --active` is a bare switch. When the model lists it
            // in the review block it shows up as { Name = "active", Value = "true" }
            // and renders as "active true", which reads as if the user must type
            // "true". Scoped to profile-show so no other command's "active" is
            // touched. Drops the value in the review block, and collapses
            // "--active true" -> "--active" in the step command.
            new(
                Name: "profile-show-active-switch",
                Why: "profile show --active is a switch; 'active true' misleads users into typing 'true'.",
                RequiresCommand: "profile show",
                Flag: "active"),
        };

        /// <summary>
        /// Return a cleaned copy of <paramref name="plan"/>. Never mutates the input;
        /// safe to call unconditionally (a no-op when no rule's command is present).
        /// </summary>
        public static ConsoleApprovalPrompt.PlanContent Sanitize(ConsoleApprovalPrompt.PlanContent plan)
        {
            var steps = plan.Steps;
            var defaults = plan.Defaults;

            foreach (var rule in Rules)
            {
                // Precise gate: only apply this rule if its command actually appears
                // in the plan's steps. No command match → this rule does nothing.
                if (!PlanMentionsCommand(steps, rule.RequiresCommand))
                {
                    continue;
                }

                // Review block: a default whose Name is the rule's flag and Value is
                // "true" → blank the value so it renders as the bare flag.
                if (defaults is not null)
                {
                    defaults = defaults
                        .Select(d => IsSwitchTrue(d, rule.Flag) ? d with { Value = string.Empty } : d)
                        .ToList();
                }

                // Step command: "--<flag> true" -> "--<flag>".
                if (steps is not null)
                {
                    var pattern = $@"(--{Regex.Escape(rule.Flag)})\s+true\b";
                    steps = steps
                        .Select(s => s with { Cmd = Regex.Replace(s.Cmd, pattern, "$1", RegexOptions.IgnoreCase) })
                        .ToList();
                }
            }

            return plan with { Steps = steps, Defaults = defaults };
        }

        // True when any step's command contains the given command phrase (e.g.
        // "profile show"). Whitespace-normalized so "az   profile show --x" matches.
        private static bool PlanMentionsCommand(
            IReadOnlyList<ConsoleApprovalPrompt.PlanStep>? steps, string commandPhrase)
        {
            if (steps is null) return false;
            foreach (var s in steps)
            {
                var normalized = Regex.Replace(s.Cmd ?? string.Empty, @"\s+", " ");
                if (normalized.Contains(commandPhrase, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        // A default that is the given switch flag mis-listed with Value "true".
        // Name is compared with any leading dashes stripped ("active" or "--active").
        private static bool IsSwitchTrue(ConsoleApprovalPrompt.ChosenDefault d, string flag) =>
            string.Equals(d.Name.TrimStart('-'), flag, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(d.Value?.Trim(), "true", StringComparison.OrdinalIgnoreCase);

        private sealed record SanitizeRule(
            string Name,
            string Why,
            string RequiresCommand,
            string Flag);
    }
}
