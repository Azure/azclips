using Core.Agent.Hosting;

namespace Core.Agent.Plan;

/// <summary>
/// Restores the <c>az</c> program name on plan steps that call an azclips command
/// group without it. The Agent receives the failed command without the program name
/// (for example <c>rest --method fetch ...</c>), and the model sometimes copies that
/// form into its plan, so the step would not run as written. Pure, for unit tests.
/// </summary>
internal static class PlanCommandNormalizer
{
    // Built-in azclips CLI command groups. A command group added by an installed
    // extension is still covered when it is the failed command's own verb.
    private static readonly HashSet<string> BuiltInVerbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "account", "agent", "config", "deployment", "disk", "extension", "group", "identity", "login",
        "logout", "msgraph", "network", "profile", "rest", "role", "troubleshoot", "version", "vm",
    };

    internal static IReadOnlyList<ConsoleApprovalPrompt.PlanStep>? EnsureAzPrefix(
        IReadOnlyList<ConsoleApprovalPrompt.PlanStep>? steps, string? failedCommandVerb)
    {
        if (steps is null) return null;

        List<ConsoleApprovalPrompt.PlanStep>? fixedSteps = null;
        for (var i = 0; i < steps.Count; i++)
        {
            var command = steps[i].Cmd.TrimStart();
            var verb = VerbOf(command);
            if (verb is null || string.Equals(verb, "az", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!BuiltInVerbs.Contains(verb) && !string.Equals(verb, failedCommandVerb, StringComparison.OrdinalIgnoreCase))
                continue;

            fixedSteps ??= new List<ConsoleApprovalPrompt.PlanStep>(steps);
            fixedSteps[i] = steps[i] with { Cmd = "az " + command };
        }
        return fixedSteps ?? steps;
    }

    // First word of a command line, or null when it is empty or starts with an option.
    internal static string? VerbOf(string? command)
    {
        if (string.IsNullOrWhiteSpace(command)) return null;
        var trimmed = command.TrimStart();
        var end = trimmed.IndexOfAny([' ', '\t']);
        var first = end < 0 ? trimmed : trimmed[..end];
        return first.StartsWith('-') ? null : first;
    }
}
