using System.Text.Json;
using Core.Agent.Hosting;

namespace Core.Agent.Plan;

/// <summary>
/// Restores the <c>az</c> program name on plan steps that call an azclips command
/// without it. The Agent receives the failed command without the program name
/// (for example <c>rest --method fetch ...</c>), and the model sometimes copies that
/// form into its plan, so the step would not run as written.
///
/// A step counts as an azclips command only when its words before the first option
/// are a full command name from the live help catalog (<c>az agent tool docs list</c>, the
/// <c>list_commands</c> source), followed by at most one value. The catalog lists every
/// installed extension, so new command groups and extensions count without a code
/// change. Prose ("Use a strong password ...", "network security group must ...",
/// "login with an account that ...") and other programs (<c>kubectl ...</c>) stay
/// unchanged. Pure, for unit tests.
/// </summary>
internal static class PlanCommandNormalizer
{
    private const int MaxWordsAfterCommandName = 1;

    // True when some step does not start with `az`: only then is the catalog needed.
    internal static bool NeedsCatalog(IReadOnlyList<ConsoleApprovalPrompt.PlanStep>? steps) =>
        steps?.Any(step => !StartsWithAz(step.Cmd) && LeadingWords(step.Cmd).Length > 0) ?? false;

    internal static IReadOnlyList<ConsoleApprovalPrompt.PlanStep>? EnsureAzPrefix(
        IReadOnlyList<ConsoleApprovalPrompt.PlanStep>? steps, IReadOnlyCollection<string> commandNames)
    {
        if (steps is null) return null;

        List<ConsoleApprovalPrompt.PlanStep>? fixedSteps = null;
        for (var i = 0; i < steps.Count; i++)
        {
            var command = steps[i].Cmd.TrimStart();
            if (StartsWithAz(command) || MatchCommandName(command, commandNames) is null)
                continue;

            fixedSteps ??= new List<ConsoleApprovalPrompt.PlanStep>(steps);
            fixedSteps[i] = steps[i] with { Cmd = "az " + command };
        }
        return fixedSteps ?? steps;
    }

    // The longest catalog command name that the command's words before its first
    // option start with ("deployment group create" for "deployment group create
    // --resource-group g"), or null. Whole words only, and case-sensitive like the
    // catalog names, so "network security group must ..." or "Login with ..." do not
    // match "network vnet create" or "login".
    //
    // Some commands take values without an option name ("extension add aks",
    // "config set <key> <value>"), and the catalog does not say how many. Allow one
    // such word after the name: it covers the one-value commands and still rejects
    // prose that starts with a command ("login with an account that ..."). A step
    // with more words before its first option keeps its text.
    internal static string? MatchCommandName(string? command, IReadOnlyCollection<string> commandNames)
    {
        var words = LeadingWords(command);
        string? match = null;
        var matchLength = 0;
        foreach (var name in commandNames)
        {
            var nameWords = name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (nameWords.Length <= matchLength || nameWords.Length > words.Length ||
                words.Length - nameWords.Length > MaxWordsAfterCommandName)
                continue;
            if (!nameWords.AsSpan().SequenceEqual(words.AsSpan(0, nameWords.Length)))
                continue;

            match = name;
            matchLength = nameWords.Length;
        }
        return match;
    }

    // The CLI command names (cliName) in the JSON that `az agent tool docs list` prints,
    // skipping commands that only have a PowerShell name. Null when the output is an
    // error payload (shell_out_timeout, ...) or not a catalog.
    internal static IReadOnlyList<string>? ReadCommandNames(string? catalogJson)
    {
        if (string.IsNullOrWhiteSpace(catalogJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(catalogJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty("commands", out var commands) ||
                commands.ValueKind != JsonValueKind.Array)
                return null;

            var names = new List<string>();
            foreach (var entry in commands.EnumerateArray())
            {
                if (entry.ValueKind == JsonValueKind.Object &&
                    entry.TryGetProperty("cliName", out var cliName) &&
                    cliName.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(cliName.GetString()))
                {
                    names.Add(cliName.GetString()!.Trim());
                }
            }
            return names;
        }
        catch (JsonException)
        {
            return null;
        }
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

    // True when the command already starts with the `az` program name.
    internal static bool StartsWithAz(string? command) =>
        string.Equals(VerbOf(command), "az", StringComparison.OrdinalIgnoreCase);

    // The words before the first option (a word that starts with '-').
    private static string[] LeadingWords(string? command) =>
        (command ?? string.Empty)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .TakeWhile(word => !word.StartsWith('-'))
            .ToArray();
}
