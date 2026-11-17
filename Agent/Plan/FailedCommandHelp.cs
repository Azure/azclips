using System.Text.Json;
using Core.Agent.Tools.Help;

namespace Core.Agent.Plan;

/// <summary>
/// Builds the "here's the help for the command you failed on" context block
/// the host injects at session start, so the agent doesn't spend a turn looking
/// up the command it was triggered by.
/// </summary>
internal static class FailedCommandHelp
{
    // Builds the context block. Conservative by design — only fires when the
    // failed input actually looks like an az command invocation. Natural-language
    // triggers ("create me a VM") or odd input fall through to null (no
    // injection), and the agent just uses its tools as before — never worse
    // than today.
    //
    // Goes through HelpTools.LookupCommandAsync, the same path the get_command
    // tool uses, so they can't drift.
    internal static async Task<string?> BuildContextAsync(
        HelpTools helpTools,
        string? failedCommand,
        CancellationToken ct = default)
    {
        var name = ExtractCommandName(failedCommand);
        if (name is null) return null;

        // Expected operational failures are already structured error payloads.
        // Skip those, but let cancellation and unexpected defects reach the host.
        var help = await helpTools.LookupCommandAsync(name, ct).ConfigureAwait(false);
        if (IsErrorPayload(help))
            return null;
        return $"Help for the command you were triggered by (`{name}`), pre-fetched so you don't need to call get_command for it:\n{help}";
    }

    // True when the get_command JSON carries any "error" field — whether the
    // lookup returned not_found, or the underlying shell-out reported a
    // spawn/timeout/exit failure. Parses the JSON and checks the "error"
    // property directly so a reformatted payload or the literal "error" text
    // appearing inside legitimate help content can't fool it.
    private static bool IsErrorPayload(string helpJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(helpJson);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("error", out var err)
                && err.ValueKind == JsonValueKind.String
                && !string.IsNullOrEmpty(err.GetString());
        }
        catch (JsonException)
        {
            // Malformed help is not trustworthy context.
            return true;
        }
    }

    // Pulls the command verb out of a failed az invocation: the leading run of
    // command-name tokens (lowercase letters/digits/hyphens) up to the first flag.
    // Returns null when the input doesn't look like a command (natural language,
    // empty, starts with a flag) — the caller then skips pre-fetch entirely.
    internal static string? ExtractCommandName(string? failedCommand)
    {
        if (string.IsNullOrWhiteSpace(failedCommand)) return null;

        var verb = new List<string>();
        foreach (var token in failedCommand.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.StartsWith('-')) break;                 // first flag ends the verb
            if (!IsCommandToken(token)) return null;          // non-command token (e.g. CJK / punctuation) → not a command
            verb.Add(token);
            if (verb.Count >= 4) break;                       // azclips verbs are short; cap to avoid eating args
        }
        return verb.Count > 0 ? string.Join(' ', verb) : null;
    }

    // A command-name token is lowercase ascii letters/digits/hyphens (e.g. "vm",
    // "create", "create-basic"). Anything else (uppercase value, path, CJK,
    // quotes) means the input isn't a clean command invocation.
    private static bool IsCommandToken(string token) =>
        token.Length > 0 && token.All(c => (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-');
}
